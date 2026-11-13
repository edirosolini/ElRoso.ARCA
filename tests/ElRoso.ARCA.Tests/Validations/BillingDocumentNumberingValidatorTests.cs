// EN: Tests for BillingDocumentNumberingValidator - the top-level validator for billing requests.
// ES: Tests para BillingDocumentNumberingValidator - el validator de tope del request de facturación.
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

namespace ElRoso.ARCA.Tests.Validations;

public class BillingDocumentNumberingValidatorTests
{
    private readonly BillingDocumentNumberingValidator validator = new();

    private static BillingDocumentNumberingRequest ValidFA() => new()
    {
        BillingDocumentType = BillingDocumentTypeARCAEnum.FA,
        BillingDocumentBookPrefix = 1,
        BillingDocumentDate = new DateTime(2026, 1, 15),
        Currency = "Pesos",
        ExchangeRate = 1,
        ConceptType = ConceptTypeARCAEnum.Products,
        AmountTax = 100,
        BillingDocumentNumberingTaxes = [new() { PercentageTax = "21.00", BaseAmount = 100, Amount = 21 }],
        IssuingCompany = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
        Client = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 30987654321 },
    };

    [Fact]
    public void Valid_when_FA_with_all_required_fields()
    {
        var result = validator.Validate(ValidFA());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_when_credit_note_without_associated_documents()
    {
        var request = ValidFA();
        request.BillingDocumentType = BillingDocumentTypeARCAEnum.NCA;
        request.BillingDocumentNumberingAssociateds = null;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BillingDocumentNumberingRequest.BillingDocumentNumberingAssociateds));
    }

    [Fact]
    public void Valid_when_credit_note_with_associated_period()
    {
        var request = ValidFA();
        request.BillingDocumentType = BillingDocumentTypeARCAEnum.NCA;
        request.BillingDocumentNumberingAssociatedPeriod = new()
        {
            DateFrom = new DateTime(2026, 1, 1),
            DateTo = new DateTime(2026, 1, 31),
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_when_associated_period_dates_are_reversed()
    {
        var request = ValidFA();
        request.BillingDocumentType = BillingDocumentTypeARCAEnum.NCA;
        request.BillingDocumentNumberingAssociatedPeriod = new()
        {
            DateFrom = new DateTime(2026, 2, 1),
            DateTo = new DateTime(2026, 1, 31),
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BillingDocumentNumberingRequest.BillingDocumentNumberingAssociatedPeriod));
    }

    [Fact]
    public void Invalid_when_associated_period_has_default_date()
    {
        var request = ValidFA();
        request.BillingDocumentType = BillingDocumentTypeARCAEnum.NCA;
        request.BillingDocumentNumberingAssociatedPeriod = new() { DateTo = new DateTime(2026, 1, 31) };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Valid_when_credit_note_with_associated_documents()
    {
        var request = ValidFA();
        request.BillingDocumentType = BillingDocumentTypeARCAEnum.NCA;
        request.BillingDocumentNumberingAssociateds =
        [
            new()
            {
                BillingDocumentType = BillingDocumentTypeARCAEnum.FA,
                BillingDocumentNumber = 1,
                BillingDocumentBookPrefix = 1,
                BillingDocumentDate = new DateTime(2026, 1, 10),
            }
        ];

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_when_export_invoice_without_BillingDocumentId()
    {
        var request = ValidFA();
        request.BillingDocumentType = BillingDocumentTypeARCAEnum.InvoiceExport;
        request.BillingDocumentId = null;
        request.Items = [new() { ItemDescription = "x", Amount = 100 }];

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BillingDocumentNumberingRequest.BillingDocumentId));
    }

    [Fact]
    public void Invalid_when_export_invoice_without_items()
    {
        var request = ValidFA();
        request.BillingDocumentType = BillingDocumentTypeARCAEnum.InvoiceExport;
        request.BillingDocumentId = 1;
        request.Items = null;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BillingDocumentNumberingRequest.Items));
    }

    [Fact]
    public void Invalid_when_service_concept_without_date_range()
    {
        var request = ValidFA();
        request.ConceptType = ConceptTypeARCAEnum.Services;
        request.DateOfServicesFrom = null;
        request.DateOfServicesTo = null;
        request.PaymentDue = null;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Service invoices"));
    }

    [Fact]
    public void Valid_when_service_concept_with_full_date_range()
    {
        var request = ValidFA();
        request.ConceptType = ConceptTypeARCAEnum.Services;
        request.DateOfServicesFrom = new DateTime(2026, 1, 1);
        request.DateOfServicesTo = new DateTime(2026, 1, 31);
        request.PaymentDue = new DateTime(2026, 2, 10);

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Valid_when_export_service_with_only_PaymentDue()
    {
        // EN: Export services only require PaymentDue, not the From/To dates.
        // ES: Servicios de exportación solo requieren PaymentDue, no las fechas From/To.
        var request = ValidFA();
        request.BillingDocumentType = BillingDocumentTypeARCAEnum.InvoiceExport;
        request.BillingDocumentId = 1;
        request.Items = [new() { ItemDescription = "x", Amount = 100 }];
        request.ConceptType = ConceptTypeARCAEnum.Services;
        request.DateOfServicesFrom = null;
        request.DateOfServicesTo = null;
        request.PaymentDue = new DateTime(2026, 2, 10);
        request.BillingDocumentNumberingTaxes = null;

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_when_currency_is_not_supported()
    {
        var request = ValidFA();
        request.Currency = "Yenes";

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BillingDocumentNumberingRequest.Currency));
    }

    [Fact]
    public void Invalid_when_BillingDocumentType_is_out_of_range()
    {
        var request = ValidFA();
        request.BillingDocumentType = (BillingDocumentTypeARCAEnum)777;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_when_BillingDocumentBookPrefix_is_zero()
    {
        var request = ValidFA();
        request.BillingDocumentBookPrefix = 0;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BillingDocumentNumberingRequest.BillingDocumentBookPrefix));
    }

    [Fact]
    public void Invalid_when_IssuingCompany_is_null()
    {
        var request = ValidFA();
        request.IssuingCompany = null!;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BillingDocumentNumberingRequest.IssuingCompany));
    }

    [Fact]
    public void Invalid_when_Client_has_invalid_document_combination()
    {
        // EN: Nested validator must propagate errors.
        // ES: El validator anidado debe propagar errores.
        var request = ValidFA();
        request.Client = new ClientRequest
        {
            DocumentType = DocumentTypeARCAEnum.CUIT,
            DocumentNumber = 0,
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.StartsWith("Client."));
    }
}
