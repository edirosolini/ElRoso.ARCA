// EN: Tests for BillingDocumentNumberingTaxValidator - validates VAT lines.
// ES: Tests para BillingDocumentNumberingTaxValidator - valida líneas de IVA.
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

namespace ElRoso.ARCA.Tests.Validations;

public class BillingDocumentNumberingTaxValidatorTests
{
    private readonly BillingDocumentNumberingTaxValidator validator = new();

    [Theory]
    [InlineData("21.00")]
    [InlineData("10.50")]
    [InlineData("27.00")]
    [InlineData("5.00")]
    [InlineData("2.50")]
    [InlineData("0.00")]
    [InlineData("Exento")]
    [InlineData("No Gravado")]
    public void Valid_when_percentage_is_in_dictionary(string percentage)
    {
        var tax = new BillingDocumentNumberingTaxRequest
        {
            PercentageTax = percentage,
            BaseAmount = 100,
            Amount = 21,
        };

        var result = validator.Validate(tax);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Invalid_when_percentage_is_not_in_dictionary()
    {
        var tax = new BillingDocumentNumberingTaxRequest
        {
            PercentageTax = "99.99",
            BaseAmount = 100,
            Amount = 99.99,
        };

        var result = validator.Validate(tax);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BillingDocumentNumberingTaxRequest.PercentageTax));
    }

    [Fact]
    public void Invalid_when_BaseAmount_is_zero()
    {
        var tax = new BillingDocumentNumberingTaxRequest
        {
            PercentageTax = "21.00",
            BaseAmount = 0,
            Amount = 0,
        };

        var result = validator.Validate(tax);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BillingDocumentNumberingTaxRequest.BaseAmount));
    }

    [Fact]
    public void Invalid_when_Amount_is_negative()
    {
        var tax = new BillingDocumentNumberingTaxRequest
        {
            PercentageTax = "21.00",
            BaseAmount = 100,
            Amount = -1,
        };

        var result = validator.Validate(tax);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BillingDocumentNumberingTaxRequest.Amount));
    }
}
