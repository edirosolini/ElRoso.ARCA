// <copyright file="InvoiceVerificationRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Read;

using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

/// <summary>
/// EN: Request to verify a received invoice against ARCA (WSCDC / Constatación de Comprobantes).
/// ES: Request para validar una factura recibida contra ARCA (WSCDC).
/// </summary>
public class InvoiceVerificationRequest
{
    /// <summary>Issuing company's CUIT. / CUIT del emisor.</summary>
    public IssuingCompanyRequest IssuingCompany { get; set; } = new();

    /// <summary>Receiver's CUIT/DNI (optional). / CUIT/DNI del receptor (opcional).</summary>
    public ClientRequest? Receiver { get; set; }

    /// <summary>Voucher type (FA, FB, FC, etc). / Tipo de comprobante.</summary>
    public BillingDocumentTypeARCAEnum BillingDocumentType { get; set; }

    /// <summary>Punto de venta (book prefix).</summary>
    public int BillingDocumentBookPrefix { get; set; }

    /// <summary>Voucher number (Cbte_nro). / Número de comprobante.</summary>
    public long BillingDocumentNumber { get; set; }

    /// <summary>Voucher date. / Fecha del comprobante.</summary>
    public DateTime BillingDocumentDate { get; set; }

    /// <summary>Total amount on the voucher. / Importe total del comprobante.</summary>
    public double TotalAmount { get; set; }

    /// <summary>Authorization code (CAE/CAI/CAEA) printed on the voucher. / Código de autorización impreso en el comprobante.</summary>
    public string AuthorizationCode { get; set; } = string.Empty;

    /// <summary>Type of authorization being validated. Default: CAE. / Tipo de autorización a validar. Default: CAE.</summary>
    public AuthorizationModeARCAEnum AuthorizationMode { get; set; } = AuthorizationModeARCAEnum.CAE;

    /// <summary>CUIT que consulta: autentica y es la clave del ticket de acceso. Sin valor, se usa el del emisor.</summary>
    public long? RequesterCuit { get; set; }
}
