// <copyright file="IPadronOperations.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

/// <summary>
/// EN: Thin wrapper over the ARCA Padron A5 SOAP client.
///     Exists so consumers can be unit-tested without touching the network.
/// ES: Wrapper fino sobre el cliente SOAP del Padrón A5 de ARCA.
///     Existe para que los consumidores puedan testearse sin tocar la red.
/// </summary>
internal interface IPadronOperations
{
    Task<PadronPersonaResult> GetPersonaAsync(
        string sign,
        string token,
        long issuingCuit,
        long clientCuit,
        CancellationToken ct);
}

/// <summary>
/// EN: POCO result of a Padron query — hides WCF types from the consumer.
/// ES: Resultado POCO de una consulta al Padrón — oculta los tipos WCF del consumidor.
/// </summary>
internal sealed record PadronPersonaResult
{
    /// <summary>Errors returned by Padron, if any. / Errores devueltos por el Padrón.</summary>
    public IReadOnlyList<string>? Errors { get; init; }

    /// <summary>Resolved client name (razón social or apellido + nombre). / Nombre del cliente resuelto.</summary>
    public string ClientName { get; init; } = string.Empty;

    /// <summary>Respuesta completa del Padrón mapeada a tipos públicos.</summary>
    public PadronPersonaResponse Persona { get; init; } = new();
}
