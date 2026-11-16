// <copyright file="IInvoiceVerificationService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Read;

using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;

/// <summary>
/// EN: Verifies received vouchers against ARCA (WSCDC). Useful when you receive an electronic
/// invoice from a vendor and want to confirm the CAE/CAI/CAEA is real and active.
/// ES: Valida comprobantes recibidos contra ARCA (WSCDC). Útil cuando recibís una factura
/// electrónica de un proveedor y querés confirmar que el CAE/CAI/CAEA es real y está vigente.
/// </summary>
public interface IInvoiceVerificationService
{
    Task<InvoiceVerificationResponse> VerifyAsync(
        InvoiceVerificationRequest request,
        CancellationToken ct = default);
}
