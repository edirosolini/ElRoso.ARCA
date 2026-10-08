// <copyright file="IInvoiceVerificationOperations.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Read;

using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

/// <summary>
/// EN: Thin wrapper over the ARCA WSCDC SOAP client (Constatación de Comprobantes).
/// ES: Wrapper fino sobre el cliente SOAP del WSCDC de ARCA.
/// </summary>
internal interface IInvoiceVerificationOperations
{
    Task<InvoiceVerificationOperationResult> VerifyAsync(
        string sign,
        string token,
        long requesterCuit,
        InvoiceVerificationRequest request,
        CancellationToken ct);
}

/// <summary>
/// EN: POCO result of a WSCDC verification — hides WCF types from the consumer.
/// ES: Resultado POCO de una verificación WSCDC — oculta los tipos WCF del consumidor.
/// </summary>
internal sealed record InvoiceVerificationOperationResult
{
    public bool IsAuthorized { get; init; }

    public string? Resultado { get; init; }

    public DateTime? ProcessedDate { get; init; }

    public IReadOnlyList<string> Observations { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];
}
