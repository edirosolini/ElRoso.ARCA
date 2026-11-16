// <copyright file="BillingDocumentNumberingResponse.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using System.Text;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;
using Newtonsoft.Json;

public class BillingDocumentNumberingResponse
{
    // --- Result ---

    public bool Result { get; set; }

    public string? CAE { get; set; }

    public int BillingDocumentNumber { get; set; }

    public DateTime? BillingDocumentBookExpirationDate { get; set; }

    /// <summary>
    /// Business errors returned by ARCA (observaciones, códigos de error).
    /// Errores de negocio devueltos por ARCA.
    /// </summary>
    public List<string> Errors { get; set; } = [];

    // --- Echo of request data (for QR generation and traceability) ---
    // --- Eco de los datos del request (para QR y trazabilidad) ---

    public BillingDocumentTypeARCAEnum BillingDocumentType { get; set; }

    public DateTime BillingDocumentDate { get; set; }

    public int BillingDocumentBookPrefix { get; set; }

    public string Currency { get; set; } = string.Empty;

    public double ExchangeRate { get; set; }

    public double Total { get; set; }

    public int Version { get; set; } = 1;

    public IssuingCompanyRequest? IssuingCompany { get; set; }

    public ClientRequest? Client { get; set; }

    // --- Factory from request ---

    public static BillingDocumentNumberingResponse FromRequest(BillingDocumentNumberingRequest request) =>
        new()
        {
            BillingDocumentType = request.BillingDocumentType,
            BillingDocumentDate = request.BillingDocumentDate,
            BillingDocumentBookPrefix = request.BillingDocumentBookPrefix,
            Currency = request.Currency,
            ExchangeRate = request.ExchangeRate,
            Total = request.Total,
            Version = request.Version,
            IssuingCompany = request.IssuingCompany,
            Client = request.Client,
        };

    /// <summary>
    /// Generates the ARCA QR code URL for printing on the invoice.
    /// Genera la URL del código QR de ARCA para imprimir en el comprobante.
    /// </summary>
    public string? QRCode()
    {
        if (string.IsNullOrEmpty(CAE) || IssuingCompany is null || Client is null)
            return null;

        var payload = new
        {
            ver = Version,
            fecha = BillingDocumentDate.ToString("yyyy-MM-dd"),
            cuit = IssuingCompany.DocumentNumber,
            ptoVta = BillingDocumentBookPrefix,
            tipoCmp = (int)BillingDocumentType,
            nroCmp = BillingDocumentNumber,
            importe = Total,
            moneda = Currency,
            ctz = ExchangeRate,
            tipoDocRec = (int)Client.DocumentType,
            nroDocRec = Client.DocumentNumber,
            tipoCodAut = "E",
            codAut = Convert.ToInt64(CAE),
        };

        var json = JsonConvert.SerializeObject(payload);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return $"https://www.afip.gob.ar/fe/qr/?p={base64}";
    }
}
