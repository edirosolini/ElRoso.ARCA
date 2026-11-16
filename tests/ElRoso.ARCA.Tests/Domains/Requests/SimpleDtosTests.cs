// EN: Trivial tests for small DTOs/requests to keep coverage at production threshold.
// ES: Tests triviales para DTOs/requests pequeños para mantener cobertura en threshold de producción.
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

namespace ElRoso.ARCA.Tests.Domains.Requests;

public class SimpleDtosTests
{
    [Fact]
    public void LoginTicketRequest_should_have_well_formed_XmlTemplate()
    {
        var request = new LoginTicketRequest();

        Assert.Contains("<loginTicketRequest>", request.XmlTemplate);
        Assert.Contains("<header>", request.XmlTemplate);
        Assert.Contains("<uniqueId>", request.XmlTemplate);
        Assert.Contains("<generationTime>", request.XmlTemplate);
        Assert.Contains("<expirationTime>", request.XmlTemplate);
        Assert.Contains("<service>", request.XmlTemplate);
    }

    [Fact]
    public void LoginTicketRequest_Service_should_default_to_empty_and_be_settable()
    {
        var request = new LoginTicketRequest();

        Assert.Empty(request.Service);

        request.Service = "wsfe";
        Assert.Equal("wsfe", request.Service);
    }

    [Fact]
    public void IssuingCompanyRequest_should_default_to_zero_and_be_settable()
    {
        var company = new IssuingCompanyRequest();

        Assert.Equal(0, company.DocumentNumber);

        company.DocumentType = DocumentTypeARCAEnum.CUIT;
        company.DocumentNumber = 20123456789;
        company.VATCondition = VATConditionARCAEnum.RESPONSABLE_INSCRIPTO;

        Assert.Equal(DocumentTypeARCAEnum.CUIT, company.DocumentType);
        Assert.Equal(20123456789, company.DocumentNumber);
        Assert.Equal(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO, company.VATCondition);
    }

    [Fact]
    public void BillingDocumentNumberingAssociatedRequest_should_hold_all_fields()
    {
        var associated = new BillingDocumentNumberingAssociatedRequest
        {
            BillingDocumentBookPrefix = 1,
            BillingDocumentDate = new DateTime(2026, 1, 15),
            BillingDocumentNumber = 42,
            BillingDocumentType = BillingDocumentTypeARCAEnum.FA,
        };

        Assert.Equal(1, associated.BillingDocumentBookPrefix);
        Assert.Equal(new DateTime(2026, 1, 15), associated.BillingDocumentDate);
        Assert.Equal(42, associated.BillingDocumentNumber);
        Assert.Equal(BillingDocumentTypeARCAEnum.FA, associated.BillingDocumentType);
    }

    [Fact]
    public void ItemRequest_should_hold_description_and_amount()
    {
        var item = new ItemRequest
        {
            ItemDescription = "Software license",
            Amount = 1000.50,
        };

        Assert.Equal("Software license", item.ItemDescription);
        Assert.Equal(1000.50, item.Amount);
    }

    [Fact]
    public void BillingDocumentNumberingOtherTaxRequest_should_hold_all_fields()
    {
        var tax = new BillingDocumentNumberingOtherTaxRequest
        {
            Description = "Impuestos internos",
            BaseAmount = 100,
            PercentageTax = 3,
            Amount = 3,
        };

        Assert.Equal("Impuestos internos", tax.Description);
        Assert.Equal(100, tax.BaseAmount);
        Assert.Equal(3, tax.PercentageTax);
        Assert.Equal(3, tax.Amount);
    }
}
