// <copyright file="IBillingDocumentNumberingService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing
{
    using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

    /// <summary>
    /// Requests a CAE (Electronic Authorization Code) from ARCA for a billing document.
    /// Solicita un CAE (Código de Autorización Electrónico) a ARCA para un comprobante.
    /// </summary>
    public interface IBillingDocumentNumberingService
    {
        /// <summary>
        /// Validates the request and obtains a CAE from ARCA.
        /// Valida el request y obtiene un CAE de ARCA.
        /// </summary>
        /// <param name="request">Billing document to authorize.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Response with CAE and document number, or error list.</returns>
        Task<BillingDocumentNumberingResponse> AuthorizeAsync(
            BillingDocumentNumberingRequest request,
            CancellationToken ct = default);

        /// <summary>
        /// EN: Returns the last voucher number ARCA authorized for a given type + point of sale.
        ///     Compare it against your own last persisted number: if ARCA is ahead, an
        ///     authorization was granted that you never stored — re-authorizing would issue a
        ///     duplicate CAE for the same sale. Wraps WSFEv1 FECompUltimoAutorizado.
        /// ES: Devuelve el último número de comprobante que ARCA autorizó para un tipo + punto
        ///     de venta. Comparalo contra tu último número persistido: si ARCA va adelante, se
        ///     otorgó una autorización que nunca guardaste — re-autorizar emitiría un CAE
        ///     duplicado por la misma venta. Envuelve WSFEv1 FECompUltimoAutorizado.
        /// </summary>
        /// <param name="issuingCompany">Issuing company (CUIT that holds the delegation).</param>
        /// <param name="billingDocumentType">Voucher type to query.</param>
        /// <param name="billingDocumentBookPrefix">Point of sale to query.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Last authorized number; 0 when the series has no vouchers yet.</returns>
        Task<int> GetLastAuthorizedNumberAsync(
            IssuingCompanyRequest issuingCompany,
            BillingDocumentTypeARCAEnum billingDocumentType,
            int billingDocumentBookPrefix,
            CancellationToken ct = default);

        /// <summary>
        /// EN: Retrieves a voucher ARCA already authorized, so its CAE can be reconciled into a
        ///     local record without re-emitting. Wraps WSFEv1 FECompConsultar.
        /// ES: Recupera un comprobante que ARCA ya autorizó, para poder reconciliar su CAE en un
        ///     registro local sin volver a emitir. Envuelve WSFEv1 FECompConsultar.
        /// </summary>
        /// <param name="issuingCompany">Issuing company (CUIT that holds the delegation).</param>
        /// <param name="billingDocumentType">Voucher type to query.</param>
        /// <param name="billingDocumentBookPrefix">Point of sale to query.</param>
        /// <param name="billingDocumentNumber">Voucher number to query.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The authorized voucher, or errors when ARCA rejects the query.</returns>
        Task<AuthorizedBillingDocumentResponse> GetAuthorizedAsync(
            IssuingCompanyRequest issuingCompany,
            BillingDocumentTypeARCAEnum billingDocumentType,
            int billingDocumentBookPrefix,
            long billingDocumentNumber,
            CancellationToken ct = default);

        /// <summary>
        /// Lista los puntos de venta de webservice del CUIT emisor. Envuelve WSFEv1 FEParamGetPtosVenta.
        /// </summary>
        /// <param name="issuingCompany">Issuing company (CUIT that holds the delegation).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Puntos de venta informados por ARCA; lista vacía si no tiene ninguno (código 602).</returns>
        /// <exception cref="ARCAServiceException">ARCA rechaza la consulta; <c>ErrorCode</c> lleva su código (600 = CUIT sin delegar).</exception>
        Task<IReadOnlyList<PointOfSaleResponse>> GetPointsOfSaleAsync(
            IssuingCompanyRequest issuingCompany,
            CancellationToken ct = default);
    }
}