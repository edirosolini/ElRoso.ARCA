// <copyright file="PadronOperations.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using System.ServiceModel;
using ElRoso.ARCA.Core;

internal sealed class PadronOperations : IPadronOperations
{
    private const string PersonaNotFoundMessage = "No existe persona";
    private const int TaxMonotributo = 20;
    private const int TaxVAT = 30;
    private const int TaxVATExempt = 32;
    private const int TaxVATNotReached = 34;

    private readonly ARCAOptions options;

    public PadronOperations(ARCAOptions options)
    {
        this.options = options;
    }

    public async Task<PadronPersonaResult> GetPersonaAsync(
        string sign,
        string token,
        long issuingCuit,
        long clientCuit,
        CancellationToken ct)
    {
        // EN: A caller that already walked away gets no socket opened on its behalf.
        // ES: A un llamador que ya se fue no se le abre ningún socket.
        ct.ThrowIfCancellationRequested();

        try
        {
            var client = new Padron.PersonaServiceA5Client(
                Padron.PersonaServiceA5Client.EndpointConfiguration.PersonaServiceA5Port,
                options.PadronUrl);
            client.InnerChannel.OperationTimeout = TimeSpan.FromSeconds(options.SoapTimeoutSeconds);

            var resp = await SoapInvoker.InvokeAsync(
                client,
                () => client.getPersonaAsync(new Padron.getPersona
                {
                    sign = sign,
                    token = token,
                    cuitRepresentada = issuingCuit,
                    idPersona = clientCuit,
                }),
                ct);

            if (resp.personaReturn.errorConstancia != null)
            {
                var errors = resp.personaReturn.errorConstancia.error
                    .Select(e => $"{e} - CUIT: {clientCuit}")
                    .ToList();
                return new PadronPersonaResult
                {
                    Errors = errors,
                    Persona = MapPersona(resp.personaReturn, clientCuit),
                };
            }

            var datos = resp.personaReturn.datosGenerales;
            var name = datos.tipoPersona == "FISICA"
                ? $"{datos.apellido} {datos.nombre}"
                : datos.razonSocial;

            return new PadronPersonaResult
            {
                ClientName = name,
                Persona = MapPersona(resp.personaReturn, clientCuit),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ARCAServiceException("ARCA Padron A5 getPersona failed.", ex);
        }
    }

    // Mapea la respuesta WCF del Padrón a tipos públicos.
    internal static PadronPersonaResponse MapPersona(Padron.personaReturn persona, long cuit)
    {
        var general = persona.datosGenerales;
        var constancia = persona.errorConstancia;
        var regime = persona.datosRegimenGeneral;
        var monotributo = persona.datosMonotributo;

        var errors = constancia?.error?.ToList() ?? [];
        var found = general is not null || !errors.Any(e => e.Contains(PersonaNotFoundMessage, StringComparison.OrdinalIgnoreCase));

        var lastName = general?.apellido ?? constancia?.apellido;
        var firstName = general?.nombre ?? constancia?.nombre;
        var businessName = general?.razonSocial;
        var displayName = !string.IsNullOrWhiteSpace(businessName)
            ? businessName.Trim()
            : $"{lastName} {firstName}".Trim();

        var taxes = MapList(regime?.impuesto, MapTax);
        var monotributoTaxes = MapList(monotributo?.impuesto, MapTax);
        var activities = MapList(regime?.actividad, MapActivity);
        var isActive = string.Equals(general?.estadoClave, "ACTIVO", StringComparison.OrdinalIgnoreCase);
        var partialErrors = MapPartialErrors(persona);
        var vatCondition = DeriveVATCondition(monotributo is not null, taxes, monotributoTaxes);

        return new PadronPersonaResponse
        {
            Found = found,
            Errors = errors,
            PartialErrors = partialErrors,
            QueriedAt = persona.metadata is { fechaHoraSpecified: true } ? persona.metadata.fechaHora : null,
            Server = persona.metadata?.servidor,
            Cuit = general is { idPersonaSpecified: true } ? general.idPersona
                : constancia is { idPersonaSpecified: true } ? constancia.idPersona
                : cuit,
            PersonType = general?.tipoPersona,
            KeyType = general?.tipoClave,
            KeyStatus = general?.estadoClave,
            IsActive = isActive,
            LastName = lastName,
            FirstName = firstName,
            BusinessName = businessName,
            DisplayName = displayName,
            IsSuccession = general?.esSucesion?.Trim().ToUpperInvariant() switch
            {
                "SI" => true,
                "NO" => false,
                _ => null,
            },
            ContractDate = general is { fechaContratoSocialSpecified: true } ? general.fechaContratoSocial : null,
            DeathDate = general is { fechaFallecimientoSpecified: true } ? general.fechaFallecimiento : null,
            FiscalYearCloseMonth = general is { mesCierreSpecified: true } ? general.mesCierre : null,
            VATCondition = vatCondition,
            ReceiverVATConditionId = (int?)vatCondition,
            FiscalAddress = general?.domicilioFiscal is { } address ? MapAddress(address) : null,
            Dependency = general?.dependencia is { } dependency ? MapDependency(dependency) : null,
            Characterizations = MapList(general?.caracterizacion, MapCharacterization),
            Taxes = taxes,
            Activities = activities,
            MainActivity = activities.FirstOrDefault(a => a.Order == 1),
            Regimes = MapList(regime?.regimen, MapRegime),
            SelfEmployedCategory = regime?.categoriaAutonomo is { } selfEmployed ? MapCategory(selfEmployed) : null,
            MonotributoCategory = monotributo?.categoriaMonotributo is { } category ? MapCategory(category) : null,
            MonotributoActivity = monotributo?.actividadMonotributista is { } activity ? MapActivity(activity) : null,
            MonotributoActivities = MapList(monotributo?.actividad, MapActivity),
            MonotributoTaxes = monotributoTaxes,
            MonotributoCompanyMembers = MapList(monotributo?.componenteDeSociedad, MapCompanyMember),
        };
    }

    // True si la excepción es el fault de ARCA para un CUIT inexistente.
    internal static bool IsPersonaNotFound(Exception exception) =>
        exception.InnerException is FaultException fault
        && fault.Message.Contains(PersonaNotFoundMessage, StringComparison.OrdinalIgnoreCase);

    // Deriva la condición de los impuestos 20, 30 y 32; con el 34 queda indeterminada.
    private static VATConditionARCAEnum? DeriveVATCondition(
        bool hasMonotributoBlock,
        IReadOnlyList<PadronTax> taxes,
        IReadOnlyList<PadronTax> monotributoTaxes)
    {
        if (taxes.Concat(monotributoTaxes).Any(t => t.Id == TaxVATNotReached))
        {
            return null;
        }

        if (hasMonotributoBlock || taxes.Concat(monotributoTaxes).Any(t => t.Id == TaxMonotributo))
        {
            return VATConditionARCAEnum.MONOTRIBUTO;
        }

        if (taxes.Any(t => t.Id == TaxVAT))
        {
            return VATConditionARCAEnum.RESPONSABLE_INSCRIPTO;
        }

        return taxes.Any(t => t.Id == TaxVATExempt) ? VATConditionARCAEnum.IVA_SUJETO_EXENTO : null;
    }

    private static List<PadronPartialError> MapPartialErrors(Padron.personaReturn persona)
    {
        var partials = new List<PadronPartialError>();
        if (persona.errorRegimenGeneral is { } general)
        {
            partials.Add(new PadronPartialError
            {
                Source = PadronErrorSourceEnum.GeneralRegime,
                Message = general.mensaje,
                Errors = general.error?.ToList() ?? [],
            });
        }

        if (persona.errorMonotributo is { } monotributo)
        {
            partials.Add(new PadronPartialError
            {
                Source = PadronErrorSourceEnum.Monotributo,
                Message = monotributo.mensaje,
                Errors = monotributo.error?.ToList() ?? [],
            });
        }

        return partials;
    }

    private static List<TTarget> MapList<TSource, TTarget>(TSource[]? source, Func<TSource, TTarget> map) =>
        source is null ? [] : [.. source.Where(x => x is not null).Select(map)];

    private static PadronTax MapTax(Padron.impuesto x) => new()
    {
        Id = x.idImpuestoSpecified ? x.idImpuesto : null,
        Description = x.descripcionImpuesto,
        Period = x.periodoSpecified ? x.periodo : null,
    };

    private static PadronActivity MapActivity(Padron.actividad x) => new()
    {
        Id = x.idActividadSpecified ? x.idActividad : null,
        Description = x.descripcionActividad,
        Order = x.ordenSpecified ? x.orden : null,
        Nomenclator = x.nomencladorSpecified ? x.nomenclador : null,
        Period = x.periodoSpecified ? x.periodo : null,
    };

    private static PadronRegime MapRegime(Padron.regimen x) => new()
    {
        Id = x.idRegimenSpecified ? x.idRegimen : null,
        Description = x.descripcionRegimen,
        TaxId = x.idImpuestoSpecified ? x.idImpuesto : null,
        Type = x.tipoRegimen,
        Period = x.periodoSpecified ? x.periodo : null,
    };

    private static PadronCategory MapCategory(Padron.categoria x) => new()
    {
        Id = x.idCategoriaSpecified ? x.idCategoria : null,
        Description = x.descripcionCategoria,
        TaxId = x.idImpuestoSpecified ? x.idImpuesto : null,
        Period = x.periodoSpecified ? x.periodo : null,
    };

    private static PadronCharacterization MapCharacterization(Padron.caracterizacion x) => new()
    {
        Id = x.idCaracterizacionSpecified ? x.idCaracterizacion : null,
        Description = x.descripcionCaracterizacion,
        Period = x.periodoSpecified ? x.periodo : null,
    };

    private static PadronAddress MapAddress(Padron.domicilio x) => new()
    {
        Street = x.direccion,
        Locality = x.localidad,
        ProvinceCode = x.idProvinciaSpecified ? x.idProvincia : null,
        ProvinceName = x.descripcionProvincia,
        PostalCode = x.codPostal,
        AdditionalData = x.datoAdicional,
        AdditionalDataType = x.tipoDatoAdicional,
        AddressType = x.tipoDomicilio,
    };

    private static PadronDependency MapDependency(Padron.dependencia x) => new()
    {
        Id = x.idDependenciaSpecified ? x.idDependencia : null,
        Description = x.descripcionDependencia,
        Street = x.direccion,
        Locality = x.localidad,
        ProvinceCode = x.idProvinciaSpecified ? x.idProvincia : null,
        ProvinceName = x.descripcionProvincia,
        PostalCode = x.codPostal,
    };

    private static PadronCompanyMember MapCompanyMember(Padron.relacion x) => new()
    {
        Cuit = x.idPersonaAsociadaSpecified ? x.idPersonaAsociada : null,
        LastName = x.apellidoPersonaAsociada,
        FirstName = x.nombrePersonaAsociada,
        BusinessName = x.razonSocialPersonaAsociada,
        ComponentType = x.tipoComponente,
        RelationDate = x.ffRelacionSpecified ? x.ffRelacion : null,
        ExpirationDate = x.ffVencimientoSpecified ? x.ffVencimiento : null,
    };
}
