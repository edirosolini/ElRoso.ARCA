// EN: Tests for computed properties on BillingDocumentNumberingRequest (Total, sums, rounding).
// ES: Tests para propiedades computadas de BillingDocumentNumberingRequest (Total, sumas, redondeo).
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

namespace ElRoso.ARCA.Tests.Domains.Requests;

public class BillingDocumentNumberingRequestTests
{
    [Fact]
    public void AmountTax_should_round_to_two_decimals()
    {
        var request = new BillingDocumentNumberingRequest { AmountTax = 100.567 };

        Assert.Equal(100.57, request.AmountTax);
    }

    [Fact]
    public void AmountNotTax_should_round_to_two_decimals()
    {
        var request = new BillingDocumentNumberingRequest { AmountNotTax = 50.444 };

        Assert.Equal(50.44, request.AmountNotTax);
    }

    [Fact]
    public void ExchangeRate_should_round_to_two_decimals()
    {
        var request = new BillingDocumentNumberingRequest { ExchangeRate = 1050.999 };

        Assert.Equal(1051.00, request.ExchangeRate);
    }

    [Fact]
    public void TaxAmount_should_sum_all_tax_lines()
    {
        var request = new BillingDocumentNumberingRequest
        {
            BillingDocumentNumberingTaxes =
            [
                new() { PercentageTax = "21.00", BaseAmount = 100, Amount = 21 },
                new() { PercentageTax = "10.50", BaseAmount = 100, Amount = 10.5 },
            ],
        };

        Assert.Equal(31.50, request.BillingDocumentNumberingTaxAmount);
    }

    [Fact]
    public void TaxAmount_should_be_zero_when_taxes_are_null()
    {
        var request = new BillingDocumentNumberingRequest();

        Assert.Equal(0, request.BillingDocumentNumberingTaxAmount);
    }

    [Fact]
    public void OtherTaxAmount_should_sum_all_other_tax_lines()
    {
        var request = new BillingDocumentNumberingRequest
        {
            BillingDocumentNumberingOtherTaxes =
            [
                new() { Description = "Otros", BaseAmount = 100, PercentageTax = 3, Amount = 3 },
                new() { Description = "Impuestos internos", BaseAmount = 100, PercentageTax = 5, Amount = 5 },
            ],
        };

        Assert.Equal(8, request.BillingDocumentNumberingOtherTaxAmount);
    }

    [Fact]
    public void OtherTaxAmount_should_be_zero_when_other_taxes_are_null()
    {
        var request = new BillingDocumentNumberingRequest();

        Assert.Equal(0, request.BillingDocumentNumberingOtherTaxAmount);
    }

    [Theory]
    [InlineData(BillingDocumentTypeARCAEnum.InvoiceExport)]
    [InlineData(BillingDocumentTypeARCAEnum.DebitNoteExport)]
    [InlineData(BillingDocumentTypeARCAEnum.CreditNoteExport)]
    public void Total_for_export_documents_should_equal_AmountTax_only(BillingDocumentTypeARCAEnum type)
    {
        // EN: Export documents do not break down VAT — Total equals the gross net (AmountTax field).
        // ES: Documentos de exportación no desglosan IVA — Total = AmountTax.
        var request = new BillingDocumentNumberingRequest
        {
            BillingDocumentType = type,
            AmountTax = 1000,
            AmountNotTax = 500,
            BillingDocumentNumberingTaxes = [new() { PercentageTax = "21.00", BaseAmount = 1000, Amount = 210 }],
            BillingDocumentNumberingOtherTaxes = [new() { Description = "Otros", BaseAmount = 100, PercentageTax = 3, Amount = 3 }],
        };

        Assert.Equal(1000, request.Total);
    }

    [Theory]
    [InlineData(BillingDocumentTypeARCAEnum.FC)]
    [InlineData(BillingDocumentTypeARCAEnum.NDC)]
    [InlineData(BillingDocumentTypeARCAEnum.NCC)]
    public void Total_for_C_documents_should_be_AmountTax_plus_OtherTaxes_no_VAT(BillingDocumentTypeARCAEnum type)
    {
        // EN: Class C documents (Monotributo) do not discriminate VAT — only AmountTax + Other taxes count.
        // ES: Comprobantes clase C (Monotributo) no discriminan IVA — solo AmountTax + Otros impuestos.
        var request = new BillingDocumentNumberingRequest
        {
            BillingDocumentType = type,
            AmountTax = 1000,
            BillingDocumentNumberingTaxes = [new() { PercentageTax = "21.00", BaseAmount = 1000, Amount = 210 }],
            BillingDocumentNumberingOtherTaxes = [new() { Description = "Otros", BaseAmount = 100, PercentageTax = 3, Amount = 3 }],
        };

        Assert.Equal(1003, request.Total);
    }

    [Fact]
    public void Total_for_FA_should_sum_all_components()
    {
        var request = new BillingDocumentNumberingRequest
        {
            BillingDocumentType = BillingDocumentTypeARCAEnum.FA,
            AmountTax = 1000,
            AmountNotTax = 100,
            BillingDocumentNumberingTaxes = [new() { PercentageTax = "21.00", BaseAmount = 1000, Amount = 210 }],
            BillingDocumentNumberingOtherTaxes = [new() { Description = "Otros", BaseAmount = 100, PercentageTax = 3, Amount = 3 }],
        };

        Assert.Equal(1313, request.Total);
    }

    [Fact]
    public void Default_Version_should_be_1()
    {
        var request = new BillingDocumentNumberingRequest();

        Assert.Equal(1, request.Version);
    }
}