// <copyright file="PadronPersonaResponse.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using ElRoso.ARCA.Core;

/// <summary>Resultado de una consulta al Padrón A5, sin tipos WCF.</summary>
public sealed record PadronPersonaResponse
{
    /// <summary>False cuando ARCA no tiene una persona con ese CUIT.</summary>
    public bool Found { get; init; }

    /// <summary>Errores comunes de la constancia (errorConstancia).</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Errores de un bloque puntual (régimen general o monotributo).</summary>
    public IReadOnlyList<PadronPartialError> PartialErrors { get; init; } = [];

    /// <summary>Fecha y hora de la respuesta según ARCA (metadata.fechaHora).</summary>
    public DateTime? QueriedAt { get; init; }

    /// <summary>Servidor que respondió (metadata.servidor).</summary>
    public string? Server { get; init; }

    /// <summary>CUIT consultado (idPersona).</summary>
    public long Cuit { get; init; }

    /// <summary>Tipo de persona tal como lo informa ARCA: FISICA o JURIDICA.</summary>
    public string? PersonType { get; init; }

    /// <summary>Tipo de clave (tipoClave), por ejemplo CUIT.</summary>
    public string? KeyType { get; init; }

    /// <summary>Estado de la clave tal como lo informa ARCA: ACTIVO o INACTIVO.</summary>
    public string? KeyStatus { get; init; }

    /// <summary>True cuando <see cref="KeyStatus"/> es ACTIVO.</summary>
    public bool IsActive { get; init; }

    public string? LastName { get; init; }

    public string? FirstName { get; init; }

    public string? BusinessName { get; init; }

    /// <summary>Razón social, o apellido y nombre si no hay razón social.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>True si es sucesión indivisa (esSucesion = SI); null si ARCA no lo informa.</summary>
    public bool? IsSuccession { get; init; }

    public DateTime? ContractDate { get; init; }

    public DateTime? DeathDate { get; init; }

    public int? FiscalYearCloseMonth { get; init; }

    /// <summary>Condición frente al IVA derivada de los impuestos informados; null si no se puede determinar.</summary>
    public VATConditionARCAEnum? VATCondition { get; init; }

    /// <summary>Código de condición frente al IVA del receptor (RG 5616) para WSFEv1; null si no se puede derivar.</summary>
    public int? ReceiverVATConditionId { get; init; }

    public PadronAddress? FiscalAddress { get; init; }

    /// <summary>Dependencia (agencia) de ARCA que corresponde a la persona.</summary>
    public PadronDependency? Dependency { get; init; }

    public IReadOnlyList<PadronCharacterization> Characterizations { get; init; } = [];

    /// <summary>Impuestos del régimen general.</summary>
    public IReadOnlyList<PadronTax> Taxes { get; init; } = [];

    /// <summary>Actividades del régimen general.</summary>
    public IReadOnlyList<PadronActivity> Activities { get; init; } = [];

    /// <summary>Actividad del régimen general con orden 1.</summary>
    public PadronActivity? MainActivity { get; init; }

    public IReadOnlyList<PadronRegime> Regimes { get; init; } = [];

    /// <summary>Categoría de autónomo (categoriaAutonomo).</summary>
    public PadronCategory? SelfEmployedCategory { get; init; }

    public PadronCategory? MonotributoCategory { get; init; }

    /// <summary>Actividad declarada en el monotributo (actividadMonotributista).</summary>
    public PadronActivity? MonotributoActivity { get; init; }

    public IReadOnlyList<PadronActivity> MonotributoActivities { get; init; } = [];

    public IReadOnlyList<PadronTax> MonotributoTaxes { get; init; } = [];

    /// <summary>Integrantes de la sociedad monotributista (componenteDeSociedad).</summary>
    public IReadOnlyList<PadronCompanyMember> MonotributoCompanyMembers { get; init; } = [];
}

/// <summary>Error de un bloque del Padrón A5 que no impide el resto de la respuesta.</summary>
public sealed record PadronPartialError
{
    public PadronErrorSourceEnum Source { get; init; }

    public string? Message { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>Domicilio informado por el Padrón A5.</summary>
public sealed record PadronAddress
{
    public string? Street { get; init; }

    public string? Locality { get; init; }

    public int? ProvinceCode { get; init; }

    public string? ProvinceName { get; init; }

    public string? PostalCode { get; init; }

    public string? AdditionalData { get; init; }

    public string? AdditionalDataType { get; init; }

    public string? AddressType { get; init; }
}

/// <summary>Dependencia de ARCA.</summary>
public sealed record PadronDependency
{
    public int? Id { get; init; }

    public string? Description { get; init; }

    public string? Street { get; init; }

    public string? Locality { get; init; }

    public int? ProvinceCode { get; init; }

    public string? ProvinceName { get; init; }

    public string? PostalCode { get; init; }
}

/// <summary>Impuesto inscripto.</summary>
public sealed record PadronTax
{
    public int? Id { get; init; }

    public string? Description { get; init; }

    /// <summary>Período inicial en formato AAAAMM.</summary>
    public int? Period { get; init; }
}

/// <summary>Actividad económica.</summary>
public sealed record PadronActivity
{
    public long? Id { get; init; }

    public string? Description { get; init; }

    public int? Order { get; init; }

    public int? Nomenclator { get; init; }

    /// <summary>Período inicial en formato AAAAMM.</summary>
    public int? Period { get; init; }
}

/// <summary>Régimen de un impuesto.</summary>
public sealed record PadronRegime
{
    public int? Id { get; init; }

    public string? Description { get; init; }

    public int? TaxId { get; init; }

    public string? Type { get; init; }

    /// <summary>Período inicial en formato AAAAMM.</summary>
    public int? Period { get; init; }
}

/// <summary>Categoría de un impuesto (monotributo o autónomos).</summary>
public sealed record PadronCategory
{
    public int? Id { get; init; }

    public string? Description { get; init; }

    public int? TaxId { get; init; }

    /// <summary>Período inicial en formato AAAAMM.</summary>
    public int? Period { get; init; }
}

/// <summary>Caracterización vigente.</summary>
public sealed record PadronCharacterization
{
    public int? Id { get; init; }

    public string? Description { get; init; }

    /// <summary>Período inicial en formato AAAAMM.</summary>
    public int? Period { get; init; }
}

/// <summary>Integrante de una sociedad monotributista.</summary>
public sealed record PadronCompanyMember
{
    public long? Cuit { get; init; }

    public string? LastName { get; init; }

    public string? FirstName { get; init; }

    public string? BusinessName { get; init; }

    public string? ComponentType { get; init; }

    public DateTime? RelationDate { get; init; }

    public DateTime? ExpirationDate { get; init; }
}
