// <copyright file="IWsfeOperations.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

/// <summary>
/// EN: Thin wrapper over the ARCA WSFEv1 SOAP client (domestic e-invoicing).
/// ES: Wrapper fino sobre el cliente SOAP del WSFEv1 de ARCA (facturación doméstica).
/// </summary>
internal interface IWsfeOperations
{
    /// <summary>
    /// EN: Returns the last authorized document number for the given type + book.
    /// ES: Devuelve el último número autorizado para el tipo + libro dados.
    /// </summary>
    Task<int> GetLastNumberAsync(
        string sign,
        string token,
        long cuit,
        int docType,
        int bookPrefix,
        CancellationToken ct);

    /// <summary>
    /// EN: Requests a CAE for one document. Returns POCO result with errors / observations / approval.
    /// ES: Solicita un CAE para un documento. Devuelve resultado POCO con errores / observaciones / aprobación.
    /// </summary>
    Task<WsfeCaeResult> SolicitarCaeAsync(
        string sign,
        string token,
        long cuit,
        BillingDocumentNumberingRequest doc,
        int next,
        CancellationToken ct);

    /// <summary>
    /// EN: Queries one already-authorized voucher. Returns POCO result so the consumer never
    ///     sees WCF types.
    /// ES: Consulta un comprobante ya autorizado. Devuelve POCO para que el consumidor no vea
    ///     tipos WCF.
    /// </summary>
    Task<WsfeVoucherResult> ConsultarComprobanteAsync(
        string sign,
        string token,
        long cuit,
        int docType,
        int bookPrefix,
        long number,
        CancellationToken ct);

    /// <summary>
    /// Consulta los puntos de venta de webservice del CUIT (FEParamGetPtosVenta).
    /// </summary>
    /// <param name="sign">WSAA sign.</param>
    /// <param name="token">WSAA token.</param>
    /// <param name="cuit">CUIT emisor.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Puntos de venta o errores de ARCA.</returns>
    Task<WsfePointsOfSaleResult> GetPointsOfSaleAsync(
        string sign,
        string token,
        long cuit,
        CancellationToken ct);
}

/// <summary>
/// Error de WSFEv1 con su código numérico.
/// </summary>
internal sealed record WsfeError
{
    /// <summary>Gets the ARCA error code. / Código de error de ARCA.</summary>
    public int Code { get; init; }

    /// <summary>Gets the ARCA error message. / Mensaje de ARCA.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Resultado POCO de FEParamGetPtosVenta: puntos de venta o errores de ARCA.
/// </summary>
internal sealed record WsfePointsOfSaleResult
{
    /// <summary>Gets the points of sale ARCA reported. / Puntos de venta informados por ARCA.</summary>
    public IReadOnlyList<PointOfSaleResponse> PointsOfSale { get; init; } = [];

    /// <summary>Gets the errors the query returned. / Errores devueltos por la consulta.</summary>
    public IReadOnlyList<WsfeError> Errors { get; init; } = [];
}

/// <summary>
/// EN: POCO result of a voucher query to WSFEv1 — hides WCF types from the consumer.
/// ES: Resultado POCO de una consulta de comprobante a WSFEv1 — oculta los tipos WCF.
/// </summary>
internal sealed record WsfeVoucherResult
{
    /// <summary>True when ARCA reports the voucher approved (Resultado == "A").</summary>
    public bool IsApproved { get; init; }

    /// <summary>The CAE code ARCA granted. / Código CAE otorgado por ARCA.</summary>
    public string? Cae { get; init; }

    /// <summary>CAE expiration date. / Fecha de vencimiento del CAE.</summary>
    public DateTime? CaeExpiration { get; init; }

    /// <summary>Date ARCA processed the authorization. / Fecha de proceso en ARCA.</summary>
    public DateTime? ProcessedDate { get; init; }

    /// <summary>Point of sale reported by ARCA. / Punto de venta informado por ARCA.</summary>
    public int BookPrefix { get; init; }

    /// <summary>Voucher type reported by ARCA. / Tipo de comprobante informado por ARCA.</summary>
    public int DocumentType { get; init; }

    /// <summary>Observations attached to the voucher. / Observaciones del comprobante.</summary>
    public IReadOnlyList<string> Observations { get; init; } = [];

    /// <summary>Errors returned by the query. / Errores devueltos por la consulta.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// EN: POCO result of a CAE request to WSFEv1 — hides WCF types from the consumer.
/// ES: Resultado POCO de una solicitud de CAE a WSFEv1 — oculta los tipos WCF del consumidor.
/// </summary>
internal sealed record WsfeCaeResult
{
    /// <summary>True when CAE was granted (Resultado == "A"). / True cuando el CAE fue otorgado.</summary>
    public bool IsApproved { get; init; }

    /// <summary>The CAE code returned by ARCA. / Código CAE devuelto por ARCA.</summary>
    public string? Cae { get; init; }

    /// <summary>CAE expiration date. / Fecha de vencimiento del CAE.</summary>
    public DateTime? CaeExpiration { get; init; }

    /// <summary>Errors returned by WSFEv1 (header-level or detail observations on rejection).</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}
