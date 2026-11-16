// EN: Tests for ClientValidator - validates the recipient client of a billing document.
// ES: Tests para ClientValidator - valida el cliente receptor del comprobante.
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;

namespace ElRoso.ARCA.Tests.Validations;

public class ClientValidatorTests
{
    private readonly ClientValidator validator = new();

    [Fact]
    public void Valid_when_CUIT_with_document_number()
    {
        var client = new ClientRequest
        {
            DocumentType = DocumentTypeARCAEnum.CUIT,
            DocumentNumber = 20123456789,
        };

        var result = validator.Validate(client);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Valid_when_DNI_with_document_number()
    {
        var client = new ClientRequest
        {
            DocumentType = DocumentTypeARCAEnum.DNI,
            DocumentNumber = 12345678,
        };

        var result = validator.Validate(client);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Valid_when_SIN_IDENTIFICAR_without_document_number()
    {
        // EN: "Consumidor Final" without document — allowed.
        // ES: Consumidor Final sin documento — permitido.
        var client = new ClientRequest
        {
            DocumentType = DocumentTypeARCAEnum.SIN_IDENTIFICAR,
            DocumentNumber = 0,
        };

        var result = validator.Validate(client);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Invalid_when_CUIT_without_document_number()
    {
        var client = new ClientRequest
        {
            DocumentType = DocumentTypeARCAEnum.CUIT,
            DocumentNumber = 0,
        };

        var result = validator.Validate(client);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ClientRequest.DocumentNumber));
    }

    [Fact]
    public void Invalid_when_DocumentType_is_out_of_enum_range()
    {
        var client = new ClientRequest
        {
            DocumentType = (DocumentTypeARCAEnum)999,
            DocumentNumber = 1,
        };

        var result = validator.Validate(client);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ClientRequest.DocumentType));
    }
}
