// Mapeo de la respuesta WCF del Padrón A5 a PadronPersonaResponse.
using System.ServiceModel;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;

namespace ElRoso.ARCA.Tests.Soap;

public class PadronMappingTests
{
    private const long Cuit = 20224107030;

    private static Padron.datosGenerales NaturalPerson() => new()
    {
        idPersona = Cuit,
        idPersonaSpecified = true,
        tipoPersona = "FISICA",
        tipoClave = "CUIT",
        estadoClave = "ACTIVO",
        apellido = "FRANCO AGUSTIN",
        nombre = "SEVERINO",
        esSucesion = "NO",
        mesCierre = 12,
        mesCierreSpecified = true,
        domicilioFiscal = new Padron.domicilio
        {
            direccion = "ALEM 5982",
            localidad = "BANFIELD",
            idProvincia = 1,
            idProvinciaSpecified = true,
            descripcionProvincia = "BUENOS AIRES",
            codPostal = "1828",
            datoAdicional = "PISO 2",
            tipoDatoAdicional = "OFICINA",
            tipoDomicilio = "FISCAL",
        },
    };

    private static Padron.impuesto Tax(int id, string description, int period = 200705) => new()
    {
        idImpuesto = id,
        idImpuestoSpecified = true,
        descripcionImpuesto = description,
        periodo = period,
        periodoSpecified = true,
    };

    private static Padron.personaReturn WithGeneralRegime(params Padron.impuesto[] taxes) => new()
    {
        datosGenerales = NaturalPerson(),
        datosRegimenGeneral = new Padron.datosRegimenGeneral { impuesto = taxes },
    };

    [Fact]
    public void Natural_person_should_map_identity_and_resolve_display_name_from_last_and_first_name()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(30, "IVA")), Cuit);

        Assert.True(result.Found);
        Assert.Equal(Cuit, result.Cuit);
        Assert.Equal("FISICA", result.PersonType);
        Assert.Equal("CUIT", result.KeyType);
        Assert.Equal("ACTIVO", result.KeyStatus);
        Assert.True(result.IsActive);
        Assert.Equal("FRANCO AGUSTIN", result.LastName);
        Assert.Equal("SEVERINO", result.FirstName);
        Assert.Null(result.BusinessName);
        Assert.Equal("FRANCO AGUSTIN SEVERINO", result.DisplayName);
        Assert.False(result.IsSuccession);
        Assert.Equal(12, result.FiscalYearCloseMonth);
        Assert.Empty(result.Errors);
        Assert.Empty(result.PartialErrors);
    }

    [Fact]
    public void Legal_entity_should_resolve_display_name_from_business_name()
    {
        var persona = new Padron.personaReturn
        {
            datosGenerales = new Padron.datosGenerales
            {
                idPersona = 30712345678,
                idPersonaSpecified = true,
                tipoPersona = "JURIDICA",
                razonSocial = "EMPRESA DEMO SA",
                estadoClave = "ACTIVO",
                fechaContratoSocial = new DateTime(2015, 3, 10),
                fechaContratoSocialSpecified = true,
            },
        };

        var result = PadronOperations.MapPersona(persona, 30712345678);

        Assert.Equal("JURIDICA", result.PersonType);
        Assert.Equal("EMPRESA DEMO SA", result.BusinessName);
        Assert.Equal("EMPRESA DEMO SA", result.DisplayName);
        Assert.Equal(new DateTime(2015, 3, 10), result.ContractDate);
    }

    [Fact]
    public void Registered_VAT_taxpayer_should_derive_RI_and_receiver_condition_1()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(11, "GANANCIAS PERSONAS FISICAS"), Tax(30, "IVA")), Cuit);

        Assert.Equal(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO, result.VATCondition);
        Assert.Equal(1, result.ReceiverVATConditionId);
    }

    [Fact]
    public void Exempt_taxpayer_should_derive_exempt_and_receiver_condition_4()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(32, "IVA EXENTO")), Cuit);

        Assert.Equal(VATConditionARCAEnum.IVA_SUJETO_EXENTO, result.VATCondition);
        Assert.Equal(4, result.ReceiverVATConditionId);
    }

    [Fact]
    public void Monotributo_block_should_derive_monotributo_and_receiver_condition_6()
    {
        var persona = new Padron.personaReturn
        {
            datosGenerales = NaturalPerson(),
            datosMonotributo = new Padron.datosMonotributo
            {
                categoriaMonotributo = new Padron.categoria
                {
                    idCategoria = 36,
                    idCategoriaSpecified = true,
                    descripcionCategoria = "B LOCACIONES DE SERVICIO",
                    idImpuesto = 20,
                    idImpuestoSpecified = true,
                    periodo = 201804,
                    periodoSpecified = true,
                },
                actividadMonotributista = new Padron.actividad
                {
                    idActividad = 8,
                    idActividadSpecified = true,
                    descripcionActividad = "PREST. DE SERVICIO O LOCACIÓN",
                    nomenclador = 1,
                    nomencladorSpecified = true,
                    orden = 0,
                    ordenSpecified = true,
                    periodo = 201803,
                    periodoSpecified = true,
                },
                actividad = [new Padron.actividad { idActividad = 620100, idActividadSpecified = true, descripcionActividad = "SOFTWARE" }],
                impuesto = [Tax(20, "MONOTRIBUTO", 201803)],
                componenteDeSociedad =
                [
                    new Padron.relacion
                    {
                        idPersonaAsociada = 23168373384,
                        idPersonaAsociadaSpecified = true,
                        apellidoPersonaAsociada = "PEREZ",
                        nombrePersonaAsociada = "JUAN",
                        tipoComponente = "SOCIO",
                        ffRelacion = new DateTime(2008, 11, 25),
                        ffRelacionSpecified = true,
                    },
                ],
            },
        };

        var result = PadronOperations.MapPersona(persona, Cuit);

        Assert.Equal(VATConditionARCAEnum.MONOTRIBUTO, result.VATCondition);
        Assert.Equal(6, result.ReceiverVATConditionId);
        Assert.Equivalent(new PadronCategory
        {
            Id = 36,
            Description = "B LOCACIONES DE SERVICIO",
            TaxId = 20,
            Period = 201804,
        }, result.MonotributoCategory);
        Assert.Equivalent(new PadronActivity
        {
            Id = 8,
            Description = "PREST. DE SERVICIO O LOCACIÓN",
            Nomenclator = 1,
            Order = 0,
            Period = 201803,
        }, result.MonotributoActivity);
        Assert.Equal(620100, Assert.Single(result.MonotributoActivities).Id);
        Assert.Equal(20, Assert.Single(result.MonotributoTaxes).Id);
        Assert.Equivalent(new PadronCompanyMember
        {
            Cuit = 23168373384,
            LastName = "PEREZ",
            FirstName = "JUAN",
            ComponentType = "SOCIO",
            RelationDate = new DateTime(2008, 11, 25),
        }, Assert.Single(result.MonotributoCompanyMembers));
    }

    [Fact]
    public void Monotributo_tax_20_in_general_regime_should_derive_monotributo()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(20, "MONOTRIBUTO")), Cuit);

        Assert.Equal(VATConditionARCAEnum.MONOTRIBUTO, result.VATCondition);
    }

    [Fact]
    public void Active_person_without_VAT_or_monotributo_taxes_should_not_derive_a_condition()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(11, "GANANCIAS PERSONAS FISICAS")), Cuit);

        Assert.Null(result.VATCondition);
        Assert.Null(result.ReceiverVATConditionId);
    }

    [Fact]
    public void Active_person_without_taxes_should_not_derive_a_condition()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(), Cuit);

        Assert.Null(result.VATCondition);
        Assert.Null(result.ReceiverVATConditionId);
    }

    [Fact]
    public void Active_person_with_taxes_without_id_should_not_derive_a_condition()
    {
        var persona = WithGeneralRegime(new Padron.impuesto { descripcionImpuesto = "IVA" });

        Assert.Null(PadronOperations.MapPersona(persona, Cuit).VATCondition);
    }

    [Fact]
    public void Tax_code_34_should_leave_the_condition_undetermined_and_keep_the_raw_tax()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(34, "IVA NO ALCANZADO")), Cuit);

        Assert.Null(result.VATCondition);
        Assert.Null(result.ReceiverVATConditionId);
        Assert.Equivalent(new PadronTax
        {
            Id = 34,
            Description = "IVA NO ALCANZADO",
            Period = 200705,
        }, Assert.Single(result.Taxes));
    }

    [Fact]
    public void Tax_code_34_in_monotributo_taxes_should_also_leave_the_condition_undetermined()
    {
        var persona = WithGeneralRegime(Tax(11, "GANANCIAS PERSONAS FISICAS"));
        persona.datosMonotributo = new Padron.datosMonotributo { impuesto = [Tax(34, "IVA NO ALCANZADO")] };

        Assert.Null(PadronOperations.MapPersona(persona, Cuit).VATCondition);
    }

    [Fact]
    public void Tax_code_34_should_win_over_other_VAT_taxes()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(30, "IVA"), Tax(34, "IVA NO ALCANZADO")), Cuit);

        Assert.Null(result.VATCondition);
    }

    [Fact]
    public void Inactive_person_without_VAT_taxes_should_not_derive_a_condition()
    {
        var persona = WithGeneralRegime(Tax(11, "GANANCIAS PERSONAS FISICAS"));
        persona.datosGenerales.estadoClave = "INACTIVO";

        var result = PadronOperations.MapPersona(persona, Cuit);

        Assert.Null(result.VATCondition);
        Assert.Null(result.ReceiverVATConditionId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Any_partial_error_should_not_derive_a_condition(bool generalRegimeError, bool monotributoError)
    {
        var persona = WithGeneralRegime(Tax(11, "GANANCIAS PERSONAS FISICAS"));
        if (generalRegimeError)
        {
            persona.errorRegimenGeneral = new Padron.errorRegimenGeneral { mensaje = "x", error = ["x"] };
        }

        if (monotributoError)
        {
            persona.errorMonotributo = new Padron.errorMonotributo { mensaje = "x", error = ["x"] };
        }

        var result = PadronOperations.MapPersona(persona, Cuit);

        Assert.Null(result.VATCondition);
    }

    [Fact]
    public void Unknown_person_should_not_derive_a_condition()
    {
        var persona = new Padron.personaReturn
        {
            errorConstancia = new Padron.errorConstancia { error = ["No existe persona con ese Id"] },
        };

        Assert.Null(PadronOperations.MapPersona(persona, Cuit).VATCondition);
    }

    [Fact]
    public void Inactive_key_should_report_IsActive_false_and_keep_status_text()
    {
        var persona = WithGeneralRegime(Tax(30, "IVA"));
        persona.datosGenerales.estadoClave = "INACTIVO";

        var result = PadronOperations.MapPersona(persona, Cuit);

        Assert.Equal("INACTIVO", result.KeyStatus);
        Assert.False(result.IsActive);
    }

    [Fact]
    public void ErrorConstancia_should_fill_errors_and_identity_from_the_error_block()
    {
        var persona = new Padron.personaReturn
        {
            errorConstancia = new Padron.errorConstancia
            {
                idPersona = Cuit,
                idPersonaSpecified = true,
                apellido = "FRANCO",
                nombre = "SEVERINO",
                error = ["La CUIT que ingresaste se encuentra inactiva, ingresá tu CUIT activa"],
            },
        };

        var result = PadronOperations.MapPersona(persona, Cuit);

        Assert.True(result.Found);
        Assert.Contains("inactiva", Assert.Single(result.Errors));
        Assert.Equal("FRANCO", result.LastName);
        Assert.Equal("SEVERINO", result.FirstName);
        Assert.Equal("FRANCO SEVERINO", result.DisplayName);
        Assert.False(result.IsActive);
    }

    [Fact]
    public void ErrorConstancia_with_unknown_person_should_map_to_not_found()
    {
        var persona = new Padron.personaReturn
        {
            errorConstancia = new Padron.errorConstancia
            {
                idPersona = 12345678901,
                idPersonaSpecified = true,
                error = ["No existe persona con ese Id"],
            },
        };

        var result = PadronOperations.MapPersona(persona, 12345678901);

        Assert.False(result.Found);
        Assert.Equal(12345678901, result.Cuit);
        Assert.Equal("No existe persona con ese Id", Assert.Single(result.Errors));
    }

    [Fact]
    public void Partial_regime_errors_should_be_exposed_with_their_message()
    {
        var persona = new Padron.personaReturn
        {
            datosGenerales = NaturalPerson(),
            errorRegimenGeneral = new Padron.errorRegimenGeneral
            {
                mensaje = "No cumple con las condiciones para enviar datos del regimen general",
                error = ["Actividad económica principal inexistente"],
            },
            errorMonotributo = new Padron.errorMonotributo
            {
                mensaje = "No cumple con las condiciones para enviar datos monotributo",
                error = ["Datos de monotributo incompletos- no posee categoría."],
            },
        };

        var result = PadronOperations.MapPersona(persona, Cuit);

        Assert.True(result.Found);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.PartialErrors.Count());
        Assert.Contains(result.PartialErrors, e =>
            e.Source == PadronErrorSourceEnum.GeneralRegime
            && e.Message == "No cumple con las condiciones para enviar datos del regimen general"
            && e.Errors.SequenceEqual(["Actividad económica principal inexistente"]));
        Assert.Contains(result.PartialErrors, e =>
            e.Source == PadronErrorSourceEnum.Monotributo
            && e.Message == "No cumple con las condiciones para enviar datos monotributo"
            && e.Errors.SequenceEqual(["Datos de monotributo incompletos- no posee categoría."]));
        Assert.Null(result.VATCondition);
    }

    [Fact]
    public void Unspecified_values_should_map_to_null()
    {
        var persona = new Padron.personaReturn
        {
            datosGenerales = new Padron.datosGenerales
            {
                tipoPersona = "FISICA",
                fechaFallecimiento = new DateTime(2020, 1, 1),
                fechaContratoSocial = new DateTime(2010, 1, 1),
                mesCierre = 12,
                domicilioFiscal = new Padron.domicilio { idProvincia = 3 },
                dependencia = new Padron.dependencia { idDependencia = 272, idProvincia = 3 },
                caracterizacion = [new Padron.caracterizacion { idCaracterizacion = 61, periodo = 201801 }],
            },
            datosRegimenGeneral = new Padron.datosRegimenGeneral
            {
                impuesto = [new Padron.impuesto { idImpuesto = 30, periodo = 200705, descripcionImpuesto = "IVA" }],
                actividad = [new Padron.actividad { idActividad = 1, orden = 1, nomenclador = 883, periodo = 201311 }],
                regimen = [new Padron.regimen { idRegimen = 1, idImpuesto = 30, periodo = 201001 }],
                categoriaAutonomo = new Padron.categoria { idCategoria = 1, idImpuesto = 308, periodo = 200512 },
            },
            metadata = new Padron.metadata { fechaHora = new DateTime(2026, 1, 1) },
        };

        var result = PadronOperations.MapPersona(persona, Cuit);

        Assert.Equal(Cuit, result.Cuit);
        Assert.Null(result.DeathDate);
        Assert.Null(result.ContractDate);
        Assert.Null(result.FiscalYearCloseMonth);
        Assert.Null(result.QueriedAt);
        Assert.Null(result.IsSuccession);
        Assert.Null(result.FiscalAddress!.ProvinceCode);
        Assert.Null(result.Dependency!.Id);
        Assert.Null(result.Dependency.ProvinceCode);
        Assert.Equivalent(new PadronCharacterization(), Assert.Single(result.Characterizations));
        Assert.Equivalent(new PadronTax { Description = "IVA" }, Assert.Single(result.Taxes));
        Assert.Equivalent(new PadronActivity(), Assert.Single(result.Activities));
        Assert.Equivalent(new PadronRegime(), Assert.Single(result.Regimes));
        Assert.Equivalent(new PadronCategory(), result.SelfEmployedCategory);
        Assert.Null(result.MainActivity);
        Assert.Null(result.VATCondition);
    }

    [Fact]
    public void Full_address_dependency_metadata_and_general_regime_should_be_mapped()
    {
        var persona = WithGeneralRegime(Tax(30, "IVA"), Tax(308, "APORTES SEG.SOCIAL AUTONOMOS", 200512));
        persona.datosGenerales.esSucesion = "SI";
        persona.datosGenerales.fechaFallecimiento = new DateTime(2022, 5, 1);
        persona.datosGenerales.fechaFallecimientoSpecified = true;
        persona.datosGenerales.dependencia = new Padron.dependencia
        {
            idDependencia = 272,
            idDependenciaSpecified = true,
            descripcionDependencia = "AGENCIA-SEDE N.1 CORDOBA",
            direccion = "BOULEVARD SAN JUAN 325 PB",
            localidad = "CORDOBA",
            idProvincia = 3,
            idProvinciaSpecified = true,
            descripcionProvincia = "CORDOBA",
            codPostal = "5000",
        };
        persona.datosGenerales.caracterizacion =
        [
            new Padron.caracterizacion
            {
                idCaracterizacion = 418,
                idCaracterizacionSpecified = true,
                descripcionCaracterizacion = "EXCEPTUADO DE CONSTITUIR DFE",
                periodo = 201901,
                periodoSpecified = true,
            },
        ];
        persona.datosRegimenGeneral.actividad =
        [
            new Padron.actividad { idActividad = 11121, idActividadSpecified = true, descripcionActividad = "CULTIVO DE MAÍZ", orden = 5, ordenSpecified = true, nomenclador = 883, nomencladorSpecified = true, periodo = 201311, periodoSpecified = true },
            new Padron.actividad { idActividad = 702092, idActividadSpecified = true, descripcionActividad = "SERVICIOS DE ASESORAMIENTO", orden = 1, ordenSpecified = true, nomenclador = 883, nomencladorSpecified = true, periodo = 201311, periodoSpecified = true },
        ];
        persona.datosRegimenGeneral.regimen =
        [
            new Padron.regimen
            {
                idRegimen = 1, idRegimenSpecified = true, descripcionRegimen = "RETENCION",
                idImpuesto = 30, idImpuestoSpecified = true, tipoRegimen = "RETENCION",
                periodo = 201001, periodoSpecified = true,
            },
        ];
        persona.datosRegimenGeneral.categoriaAutonomo = new Padron.categoria
        {
            idCategoria = 1,
            idCategoriaSpecified = true,
            descripcionCategoria = "CAT I",
            idImpuesto = 308,
            idImpuestoSpecified = true,
            periodo = 200512,
            periodoSpecified = true,
        };
        persona.metadata = new Padron.metadata
        {
            fechaHora = new DateTime(2026, 10, 6, 12, 0, 0),
            fechaHoraSpecified = true,
            servidor = "aws.afip.gob.ar",
        };

        var result = PadronOperations.MapPersona(persona, Cuit);

        Assert.Equivalent(new PadronAddress
        {
            Street = "ALEM 5982",
            Locality = "BANFIELD",
            ProvinceCode = 1,
            ProvinceName = "BUENOS AIRES",
            PostalCode = "1828",
            AdditionalData = "PISO 2",
            AdditionalDataType = "OFICINA",
            AddressType = "FISCAL",
        }, result.FiscalAddress);
        Assert.Equivalent(new PadronDependency
        {
            Id = 272,
            Description = "AGENCIA-SEDE N.1 CORDOBA",
            Street = "BOULEVARD SAN JUAN 325 PB",
            Locality = "CORDOBA",
            ProvinceCode = 3,
            ProvinceName = "CORDOBA",
            PostalCode = "5000",
        }, result.Dependency);
        Assert.True(result.IsSuccession);
        Assert.Equal(new DateTime(2022, 5, 1), result.DeathDate);
        Assert.Equal(new DateTime(2026, 10, 6, 12, 0, 0), result.QueriedAt);
        Assert.Equal("aws.afip.gob.ar", result.Server);
        Assert.Equivalent(new PadronCharacterization
        {
            Id = 418,
            Description = "EXCEPTUADO DE CONSTITUIR DFE",
            Period = 201901,
        }, Assert.Single(result.Characterizations));
        Assert.Equal(new int?[] { 30, 308 }, result.Taxes.Select(t => t.Id));
        Assert.Equal(2, result.Activities.Count());
        Assert.Equivalent(new PadronActivity
        {
            Id = 702092,
            Description = "SERVICIOS DE ASESORAMIENTO",
            Order = 1,
            Nomenclator = 883,
            Period = 201311,
        }, result.MainActivity);
        Assert.Equivalent(new PadronRegime
        {
            Id = 1,
            Description = "RETENCION",
            TaxId = 30,
            Type = "RETENCION",
            Period = 201001,
        }, Assert.Single(result.Regimes));
        Assert.Equivalent(new PadronCategory
        {
            Id = 1,
            Description = "CAT I",
            TaxId = 308,
            Period = 200512,
        }, result.SelfEmployedCategory);
    }

    [Fact]
    public void IsPersonaNotFound_should_detect_the_unknown_person_fault_only()
    {
        var notFound = new ARCAServiceException("ARCA Padron A5 getPersona failed.", new FaultException("No existe persona con ese Id"));
        var otherFault = new ARCAServiceException("ARCA Padron A5 getPersona failed.", new FaultException("Error interno"));
        var network = new ARCAServiceException("ARCA Padron A5 getPersona failed.", new TimeoutException());

        Assert.True(PadronOperations.IsPersonaNotFound(notFound));
        Assert.False(PadronOperations.IsPersonaNotFound(otherFault));
        Assert.False(PadronOperations.IsPersonaNotFound(network));
    }
}
