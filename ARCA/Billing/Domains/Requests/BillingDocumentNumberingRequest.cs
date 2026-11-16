// <copyright file="BillingDocumentNumberingRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

public class BillingDocumentNumberingRequest
{
    private double amountTax;
    private double amountNotTax;
    private double exchangeRate;

    public IEnumerable<BillingDocumentNumberingAssociatedRequest>? BillingDocumentNumberingAssociateds { get; set; }

    /// <summary>
    /// Gets or sets the period associated with a credit or debit note when individual receipts are not provided.
    /// / Obtiene o establece el período asociado a una nota de crédito o débito cuando no se informan comprobantes individuales.
    /// </summary>
    public BillingDocumentNumberingAssociatedPeriodRequest? BillingDocumentNumberingAssociatedPeriod { get; set; }

    public long? BillingDocumentId { get; set; }

    public int BillingDocumentBookPrefix { get; set; }

    public ClientRequest Client { get; set; } = new();

    public ConceptTypeARCAEnum ConceptType { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime? DateOfServicesFrom { get; set; }

    public DateTime? DateOfServicesTo { get; set; }

    public DateTime BillingDocumentDate { get; set; }

    public BillingDocumentTypeARCAEnum BillingDocumentType { get; set; }

    public double ExchangeRate
    {
        get => Math.Round(exchangeRate, 2);
        set => exchangeRate = Math.Round(value, 2);
    }

    public IssuingCompanyRequest IssuingCompany { get; set; } = new();

    public IEnumerable<ItemRequest>? Items { get; set; }

    public double AmountTax
    {
        get => Math.Round(amountTax, 2);
        set => amountTax = Math.Round(value, 2);
    }

    public double AmountNotTax
    {
        get => Math.Round(amountNotTax, 2);
        set => amountNotTax = Math.Round(value, 2);
    }

    public DateTime? PaymentDue { get; set; }

    public IEnumerable<BillingDocumentNumberingOtherTaxRequest>? BillingDocumentNumberingOtherTaxes { get; set; }

    public double BillingDocumentNumberingOtherTaxAmount =>
        Math.Round(BillingDocumentNumberingOtherTaxes?.Sum(x => x.Amount) ?? 0, 2);

    public IEnumerable<BillingDocumentNumberingTaxRequest>? BillingDocumentNumberingTaxes { get; set; }

    public double BillingDocumentNumberingTaxAmount =>
        Math.Round(BillingDocumentNumberingTaxes?.Sum(x => x.Amount) ?? 0, 2);

    public double Total => BillingDocumentType switch
    {
        BillingDocumentTypeARCAEnum.InvoiceExport
        or BillingDocumentTypeARCAEnum.DebitNoteExport
        or BillingDocumentTypeARCAEnum.CreditNoteExport
            => Math.Round(AmountTax, 2),

        BillingDocumentTypeARCAEnum.FC
        or BillingDocumentTypeARCAEnum.NDC
        or BillingDocumentTypeARCAEnum.NCC
            => Math.Round(AmountTax + BillingDocumentNumberingOtherTaxAmount, 2),

        _ => Math.Round(AmountTax + AmountNotTax + BillingDocumentNumberingTaxAmount + BillingDocumentNumberingOtherTaxAmount, 2),
    };

    /// <summary>QR code schema version. / Versión del esquema del QR.</summary>
    public int Version { get; set; } = 1;
}
