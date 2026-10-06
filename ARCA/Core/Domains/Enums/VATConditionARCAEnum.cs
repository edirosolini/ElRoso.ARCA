// <copyright file="VATConditionARCAEnum.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Core;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel.DataAnnotations;

[JsonConverter(typeof(StringEnumConverter))]
public enum VATConditionARCAEnum
{
    /// <summary>IVA Responsable Inscripto (código ARCA 1).</summary>
    [Display(Name = "Responsable Inscripto")]
    RESPONSABLE_INSCRIPTO = 1,

    /// <summary>IVA Sujeto Exento (código ARCA 4).</summary>
    [Display(Name = "IVA Sujeto Exento")]
    IVA_SUJETO_EXENTO = 4,

    /// <summary>Consumidor Final (código ARCA 5).</summary>
    [Display(Name = "Consumidor Final")]
    CONSUMIDOR_FINAL = 5,

    /// <summary>Responsable Monotributo (código ARCA 6).</summary>
    [Display(Name = "Monotributo")]
    MONOTRIBUTO = 6,

    /// <summary>Sujeto No Categorizado (código ARCA 7).</summary>
    [Display(Name = "Sujeto No Categorizado")]
    SUJETO_NO_CATEGORIZADO = 7,

    /// <summary>Proveedor del Exterior (código ARCA 8).</summary>
    [Display(Name = "Proveedor del Exterior")]
    PROVEEDOR_DEL_EXTERIOR = 8,

    /// <summary>Cliente del Exterior (código ARCA 9).</summary>
    [Display(Name = "Cliente del Exterior")]
    CLIENTE_DEL_EXTERIOR = 9,

    /// <summary>IVA Liberado – Ley N° 19.640 (código ARCA 10).</summary>
    [Display(Name = "IVA Liberado – Ley N° 19.640")]
    IVA_LIBERADO_LEY_19640 = 10,

    /// <summary>Monotributista Social (código ARCA 13).</summary>
    [Display(Name = "Monotributista Social")]
    MONOTRIBUTISTA_SOCIAL = 13,

    /// <summary>IVA No Alcanzado (código ARCA 15).</summary>
    [Display(Name = "IVA No Alcanzado")]
    IVA_NO_ALCANZADO = 15,

    /// <summary>Monotributo Trabajador Independiente Promovido (código ARCA 16).</summary>
    [Display(Name = "Monotributo Trabajador Independiente Promovido")]
    MONOTRIBUTO_TRABAJADOR_INDEPENDIENTE_PROMOVIDO = 16,
}
