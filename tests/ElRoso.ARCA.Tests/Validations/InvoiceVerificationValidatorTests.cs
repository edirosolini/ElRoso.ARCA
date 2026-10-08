// EN: Tests for InvoiceVerificationValidator — local validation before hitting WSCDC.
// ES: Tests para InvoiceVerificationValidator — validación local antes de pegarle al WSCDC.
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

namespace ElRoso.ARCA.Tests.Validations;

public class InvoiceVerificationValidatorTests
{
    private readonly InvoiceVerificationValidator validator = new();

    private static InvoiceVerificationRequest ValidRequest() => new()
    {
        BillingDocumentType = BillingDocumentTypeARCAEnum.FA,
        BillingDocumentBookPrefix = 1,
        BillingDocumentNumber = 42,
        BillingDocumentDate = new DateTime(2026, 5, 1),
        TotalAmount = 1210.00,
        AuthorizationCode = "75123456789012",
        AuthorizationMode = AuthorizationModeARCAEnum.CAE,
        IssuingCompany = new IssuingCompanyRequest
        {
            DocumentType = DocumentTypeARCAEnum.CUIT,
            DocumentNumber = 20123456789,
        },
    };

    [Fact]
    public void Valid_request_should_pass()
    {
        var result = validator.Validate(ValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_when_RequesterCuit_is_zero()
    {
        var request = ValidRequest();
        request.RequesterCuit = 0;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(InvoiceVerificationRequest.RequesterCuit));
    }

    [Fact]
    public void Valid_when_RequesterCuit_is_positive()
    {
        var request = ValidRequest();
        request.RequesterCuit = 30000000000;

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_when_BookPrefix_is_zero()
    {
        var request = ValidRequest();
        request.BillingDocumentBookPrefix = 0;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(InvoiceVerificationRequest.BillingDocumentBookPrefix));
    }

    [Fact]
    public void Invalid_when_DocumentNumber_is_zero()
    {
        var request = ValidRequest();
        request.BillingDocumentNumber = 0;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(InvoiceVerificationRequest.BillingDocumentNumber));
    }

    [Fact]
    public void Invalid_when_TotalAmount_is_zero_or_negative()
    {
        var request = ValidRequest();
        request.TotalAmount = 0;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(InvoiceVerificationRequest.TotalAmount));
    }

    [Fact]
    public void Invalid_when_AuthorizationCode_is_empty()
    {
        var request = ValidRequest();
        request.AuthorizationCode = string.Empty;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(InvoiceVerificationRequest.AuthorizationCode));
    }

    [Fact]
    public void Invalid_when_BillingDocumentType_is_out_of_enum_range()
    {
        var request = ValidRequest();
        request.BillingDocumentType = (BillingDocumentTypeARCAEnum)9999;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_when_AuthorizationMode_is_out_of_enum_range()
    {
        var request = ValidRequest();
        request.AuthorizationMode = (AuthorizationModeARCAEnum)9999;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_when_IssuingCompany_DocumentNumber_is_zero()
    {
        var request = ValidRequest();
        request.IssuingCompany.DocumentNumber = 0;

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(AuthorizationModeARCAEnum.CAE)]
    [InlineData(AuthorizationModeARCAEnum.CAI)]
    [InlineData(AuthorizationModeARCAEnum.CAEA)]
    public void Valid_with_each_authorization_mode(AuthorizationModeARCAEnum mode)
    {
        var request = ValidRequest();
        request.AuthorizationMode = mode;

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}