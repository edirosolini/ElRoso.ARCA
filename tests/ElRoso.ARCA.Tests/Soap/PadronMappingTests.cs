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

        result.Found.Should().BeTrue();
        result.Cuit.Should().Be(Cuit);
        result.PersonType.Should().Be("FISICA");
        result.KeyType.Should().Be("CUIT");
        result.KeyStatus.Should().Be("ACTIVO");
        result.IsActive.Should().BeTrue();
        result.LastName.Should().Be("FRANCO AGUSTIN");
        result.FirstName.Should().Be("SEVERINO");
        result.BusinessName.Should().BeNull();
        result.DisplayName.Should().Be("FRANCO AGUSTIN SEVERINO");
        result.IsSuccession.Should().BeFalse();
        result.FiscalYearCloseMonth.Should().Be(12);
        result.Errors.Should().BeEmpty();
        result.PartialErrors.Should().BeEmpty();
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

        result.PersonType.Should().Be("JURIDICA");
        result.BusinessName.Should().Be("EMPRESA DEMO SA");
        result.DisplayName.Should().Be("EMPRESA DEMO SA");
        result.ContractDate.Should().Be(new DateTime(2015, 3, 10));
    }

    [Fact]
    public void Registered_VAT_taxpayer_should_derive_RI_and_receiver_condition_1()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(11, "GANANCIAS PERSONAS FISICAS"), Tax(30, "IVA")), Cuit);

        result.VATCondition.Should().Be(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO);
        result.ReceiverVATConditionId.Should().Be(1);
    }

    [Fact]
    public void Exempt_taxpayer_should_derive_exempt_and_receiver_condition_4()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(32, "IVA EXENTO")), Cuit);

        result.VATCondition.Should().Be(VATConditionARCAEnum.IVA_SUJETO_EXENTO);
        result.ReceiverVATConditionId.Should().Be(4);
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

        result.VATCondition.Should().Be(VATConditionARCAEnum.MONOTRIBUTO);
        result.ReceiverVATConditionId.Should().Be(6);
        result.MonotributoCategory.Should().BeEquivalentTo(new PadronCategory
        {
            Id = 36,
            Description = "B LOCACIONES DE SERVICIO",
            TaxId = 20,
            Period = 201804,
        });
        result.MonotributoActivity.Should().BeEquivalentTo(new PadronActivity
        {
            Id = 8,
            Description = "PREST. DE SERVICIO O LOCACIÓN",
            Nomenclator = 1,
            Order = 0,
            Period = 201803,
        });
        result.MonotributoActivities.Should().ContainSingle().Which.Id.Should().Be(620100);
        result.MonotributoTaxes.Should().ContainSingle().Which.Id.Should().Be(20);
        result.MonotributoCompanyMembers.Should().ContainSingle().Which.Should().BeEquivalentTo(new PadronCompanyMember
        {
            Cuit = 23168373384,
            LastName = "PEREZ",
            FirstName = "JUAN",
            ComponentType = "SOCIO",
            RelationDate = new DateTime(2008, 11, 25),
        });
    }

    [Fact]
    public void Monotributo_tax_20_in_general_regime_should_derive_monotributo()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(20, "MONOTRIBUTO")), Cuit);

        result.VATCondition.Should().Be(VATConditionARCAEnum.MONOTRIBUTO);
    }

    [Fact]
    public void Active_person_without_VAT_or_monotributo_taxes_should_derive_no_categorizado()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(11, "GANANCIAS PERSONAS FISICAS")), Cuit);

        result.VATCondition.Should().Be(VATConditionARCAEnum.SUJETO_NO_CATEGORIZADO);
        result.ReceiverVATConditionId.Should().Be(7);
    }

    [Fact]
    public void Tax_code_34_should_leave_the_condition_undetermined_and_keep_the_raw_tax()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(34, "IVA NO ALCANZADO")), Cuit);

        result.VATCondition.Should().BeNull();
        result.ReceiverVATConditionId.Should().BeNull();
        result.Taxes.Should().ContainSingle().Which.Should().BeEquivalentTo(new PadronTax
        {
            Id = 34,
            Description = "IVA NO ALCANZADO",
            Period = 200705,
        });
    }

    [Fact]
    public void Tax_code_34_in_monotributo_taxes_should_also_leave_the_condition_undetermined()
    {
        var persona = WithGeneralRegime(Tax(11, "GANANCIAS PERSONAS FISICAS"));
        persona.datosMonotributo = new Padron.datosMonotributo { impuesto = [Tax(34, "IVA NO ALCANZADO")] };

        PadronOperations.MapPersona(persona, Cuit).VATCondition.Should().BeNull();
    }

    [Fact]
    public void Tax_code_34_should_win_over_other_VAT_taxes()
    {
        var result = PadronOperations.MapPersona(WithGeneralRegime(Tax(30, "IVA"), Tax(34, "IVA NO ALCANZADO")), Cuit);

        result.VATCondition.Should().BeNull();
    }

    [Fact]
    public void Inactive_person_without_VAT_taxes_should_not_derive_a_condition()
    {
        var persona = WithGeneralRegime(Tax(11, "GANANCIAS PERSONAS FISICAS"));
        persona.datosGenerales.estadoClave = "INACTIVO";

        var result = PadronOperations.MapPersona(persona, Cuit);

        result.VATCondition.Should().BeNull();
        result.ReceiverVATConditionId.Should().BeNull();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Any_partial_error_should_block_no_categorizado(bool generalRegimeError, bool monotributoError)
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

        result.VATCondition.Should().BeNull();
    }

    [Fact]
    public void Unknown_person_should_not_derive_a_condition()
    {
        var persona = new Padron.personaReturn
        {
            errorConstancia = new Padron.errorConstancia { error = ["No existe persona con ese Id"] },
        };

        PadronOperations.MapPersona(persona, Cuit).VATCondition.Should().BeNull();
    }

    [Fact]
    public void Inactive_key_should_report_IsActive_false_and_keep_status_text()
    {
        var persona = WithGeneralRegime(Tax(30, "IVA"));
        persona.datosGenerales.estadoClave = "INACTIVO";

        var result = PadronOperations.MapPersona(persona, Cuit);

        result.KeyStatus.Should().Be("INACTIVO");
        result.IsActive.Should().BeFalse();
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

        result.Found.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Contain("inactiva");
        result.LastName.Should().Be("FRANCO");
        result.FirstName.Should().Be("SEVERINO");
        result.DisplayName.Should().Be("FRANCO SEVERINO");
        result.IsActive.Should().BeFalse();
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

        result.Found.Should().BeFalse();
        result.Cuit.Should().Be(12345678901);
        result.Errors.Should().ContainSingle().Which.Should().Be("No existe persona con ese Id");
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

        result.Found.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.PartialErrors.Should().HaveCount(2);
        result.PartialErrors.Should().ContainEquivalentOf(new PadronPartialError
        {
            Source = PadronErrorSourceEnum.GeneralRegime,
            Message = "No cumple con las condiciones para enviar datos del regimen general",
            Errors = ["Actividad económica principal inexistente"],
        });
        result.PartialErrors.Should().ContainEquivalentOf(new PadronPartialError
        {
            Source = PadronErrorSourceEnum.Monotributo,
            Message = "No cumple con las condiciones para enviar datos monotributo",
            Errors = ["Datos de monotributo incompletos- no posee categoría."],
        });
        result.VATCondition.Should().BeNull();
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

        result.Cuit.Should().Be(Cuit, "sin idPersona se usa el CUIT consultado");
        result.DeathDate.Should().BeNull();
        result.ContractDate.Should().BeNull();
        result.FiscalYearCloseMonth.Should().BeNull();
        result.QueriedAt.Should().BeNull();
        result.IsSuccession.Should().BeNull();
        result.FiscalAddress!.ProvinceCode.Should().BeNull();
        result.Dependency!.Id.Should().BeNull();
        result.Dependency.ProvinceCode.Should().BeNull();
        result.Characterizations.Should().ContainSingle().Which.Should().BeEquivalentTo(new PadronCharacterization());
        result.Taxes.Should().ContainSingle().Which.Should().BeEquivalentTo(new PadronTax { Description = "IVA" });
        result.Activities.Should().ContainSingle().Which.Should().BeEquivalentTo(new PadronActivity());
        result.Regimes.Should().ContainSingle().Which.Should().BeEquivalentTo(new PadronRegime());
        result.SelfEmployedCategory.Should().BeEquivalentTo(new PadronCategory());
        result.MainActivity.Should().BeNull("sin orden informado no hay actividad principal");
        result.VATCondition.Should().BeNull("un impuesto sin id informado no se puede clasificar");
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

        result.FiscalAddress.Should().BeEquivalentTo(new PadronAddress
        {
            Street = "ALEM 5982",
            Locality = "BANFIELD",
            ProvinceCode = 1,
            ProvinceName = "BUENOS AIRES",
            PostalCode = "1828",
            AdditionalData = "PISO 2",
            AdditionalDataType = "OFICINA",
            AddressType = "FISCAL",
        });
        result.Dependency.Should().BeEquivalentTo(new PadronDependency
        {
            Id = 272,
            Description = "AGENCIA-SEDE N.1 CORDOBA",
            Street = "BOULEVARD SAN JUAN 325 PB",
            Locality = "CORDOBA",
            ProvinceCode = 3,
            ProvinceName = "CORDOBA",
            PostalCode = "5000",
        });
        result.IsSuccession.Should().BeTrue();
        result.DeathDate.Should().Be(new DateTime(2022, 5, 1));
        result.QueriedAt.Should().Be(new DateTime(2026, 10, 6, 12, 0, 0));
        result.Server.Should().Be("aws.afip.gob.ar");
        result.Characterizations.Should().ContainSingle().Which.Should().BeEquivalentTo(new PadronCharacterization
        {
            Id = 418,
            Description = "EXCEPTUADO DE CONSTITUIR DFE",
            Period = 201901,
        });
        result.Taxes.Select(t => t.Id).Should().Equal(30, 308);
        result.Activities.Should().HaveCount(2);
        result.MainActivity.Should().BeEquivalentTo(new PadronActivity
        {
            Id = 702092,
            Description = "SERVICIOS DE ASESORAMIENTO",
            Order = 1,
            Nomenclator = 883,
            Period = 201311,
        });
        result.Regimes.Should().ContainSingle().Which.Should().BeEquivalentTo(new PadronRegime
        {
            Id = 1,
            Description = "RETENCION",
            TaxId = 30,
            Type = "RETENCION",
            Period = 201001,
        });
        result.SelfEmployedCategory.Should().BeEquivalentTo(new PadronCategory
        {
            Id = 1,
            Description = "CAT I",
            TaxId = 308,
            Period = 200512,
        });
    }

    [Fact]
    public void IsPersonaNotFound_should_detect_the_unknown_person_fault_only()
    {
        var notFound = new ARCAServiceException("ARCA Padron A5 getPersona failed.", new FaultException("No existe persona con ese Id"));
        var otherFault = new ARCAServiceException("ARCA Padron A5 getPersona failed.", new FaultException("Error interno"));
        var network = new ARCAServiceException("ARCA Padron A5 getPersona failed.", new TimeoutException());

        PadronOperations.IsPersonaNotFound(notFound).Should().BeTrue();
        PadronOperations.IsPersonaNotFound(otherFault).Should().BeFalse();
        PadronOperations.IsPersonaNotFound(network).Should().BeFalse();
    }
}
