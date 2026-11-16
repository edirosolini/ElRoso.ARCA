// <copyright file="IWsfexOperations.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;

/// <summary>
/// EN: Thin wrapper over the ARCA WSFEXv1 SOAP client (export e-invoicing).
/// ES: Wrapper fino sobre el cliente SOAP del WSFEXv1 de ARCA (facturación exportación).
/// </summary>
internal interface IWsfexOperations
{
    Task<long> GetLastNumberAsync(
        string sign,
        string token,
        long cuit,
        short docType,
        int bookPrefix,
        CancellationToken ct);

    Task<WsfexCaeResult> AuthorizeAsync(
        string sign,
        string token,
        long cuit,
        BillingDocumentNumberingRequest doc,
        long next,
        CancellationToken ct);
}

/// <summary>
/// EN: POCO result of an Authorize request to WSFEXv1.
/// ES: Resultado POCO de una solicitud Authorize a WSFEXv1.
/// </summary>
internal sealed record WsfexCaeResult
{
    public bool IsApproved { get; init; }

    public string? Cae { get; init; }

    public DateTime? CaeExpiration { get; init; }

    public int DocumentNumber { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];
}
