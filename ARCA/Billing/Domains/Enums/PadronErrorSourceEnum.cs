// <copyright file="PadronErrorSourceEnum.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

/// <summary>Bloque del Padrón A5 que no se pudo emitir.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum PadronErrorSourceEnum
{
    /// <summary>Datos del régimen general (errorRegimenGeneral).</summary>
    GeneralRegime = 1,

    /// <summary>Datos del monotributo (errorMonotributo).</summary>
    Monotributo = 2,
}
