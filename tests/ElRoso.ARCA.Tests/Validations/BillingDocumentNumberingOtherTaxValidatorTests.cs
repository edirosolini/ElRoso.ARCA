// EN: Tests for BillingDocumentNumberingOtherTaxValidator - validates non-VAT taxes (IIBB, internos, etc.).
// ES: Tests para BillingDocumentNumberingOtherTaxValidator - valida impuestos no-IVA (IIBB, internos, etc.).
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

namespace ElRoso.ARCA.Tests.Validations;

public class BillingDocumentNumberingOtherTaxValidatorTests
{
    private readonly BillingDocumentNumberingOtherTaxValidator validator = new();

    [Theory]
    [InlineData("Impuestos nacionales")]
    [InlineData("Impuestos provinciales")]
    [InlineData("Impuestos municipales")]
    [InlineData("Impuestos internos")]
    [InlineData("Otros")]
    public void Valid_when_description_is_in_dictionary(string description)
    {
        var tax = new BillingDocumentNumberingOtherTaxRequest
        {
            Description = description,
            BaseAmount = 100,
            PercentageTax = 3,
            Amount = 3,
        };

        var result = validator.Validate(tax);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Invalid_when_description_is_not_in_dictionary()
    {
        var tax = new BillingDocumentNumberingOtherTaxRequest
        {
            Description = "Algo inventado",
            BaseAmount = 100,
            PercentageTax = 3,
            Amount = 3,
        };

        var result = validator.Validate(tax);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BillingDocumentNumberingOtherTaxRequest.Description));
    }

    [Fact]
    public void Invalid_when_BaseAmount_is_zero()
    {
        var tax = new BillingDocumentNumberingOtherTaxRequest
        {
            Description = "Otros",
            BaseAmount = 0,
            PercentageTax = 3,
            Amount = 0,
        };

        var result = validator.Validate(tax);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BillingDocumentNumberingOtherTaxRequest.BaseAmount));
    }

    [Fact]
    public void Invalid_when_PercentageTax_is_zero()
    {
        var tax = new BillingDocumentNumberingOtherTaxRequest
        {
            Description = "Otros",
            BaseAmount = 100,
            PercentageTax = 0,
            Amount = 3,
        };

        var result = validator.Validate(tax);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BillingDocumentNumberingOtherTaxRequest.PercentageTax));
    }

    [Fact]
    public void Invalid_when_Amount_is_negative()
    {
        var tax = new BillingDocumentNumberingOtherTaxRequest
        {
            Description = "Otros",
            BaseAmount = 100,
            PercentageTax = 3,
            Amount = -5,
        };

        var result = validator.Validate(tax);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BillingDocumentNumberingOtherTaxRequest.Amount));
    }
}
