// <copyright file="IPadronService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

/// <summary>Consulta de personas en el Padrón A5 de ARCA (ws_sr_constancia_inscripcion).</summary>
public interface IPadronService
{
    /// <summary>Devuelve los datos de inscripción de un CUIT; <c>Found = false</c> si ARCA no lo conoce.</summary>
    /// <param name="representedCuit">CUIT que firma la consulta (titular del certificado o de la delegación).</param>
    /// <param name="cuit">CUIT a consultar.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Datos de la persona sin tipos WCF.</returns>
    Task<PadronPersonaResponse> GetPersonaAsync(long representedCuit, long cuit, CancellationToken ct = default);
}
