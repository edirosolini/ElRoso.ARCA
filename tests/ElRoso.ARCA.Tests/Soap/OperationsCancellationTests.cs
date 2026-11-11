// EN: Every SOAP wrapper must honour the CancellationToken it receives, and surface the
//     cancellation as OperationCanceledException instead of burying it in an ARCAServiceException.
// ES: Cada wrapper SOAP tiene que respetar el CancellationToken que recibe, y exponer la
//     cancelacion como OperationCanceledException en vez de enterrarla en un ARCAServiceException.
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElRoso.ARCA.Tests.Soap;

public class OperationsCancellationTests
{
    private static readonly ARCAOptions Options = new() { IsProduction = false };

    // EN: Already cancelled — no socket should be opened towards ARCA at all.
    // ES: Ya cancelado — no se tiene que abrir ningun socket hacia ARCA.
    private static CancellationToken Cancelled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        return cts.Token;
    }

    private static BillingDocumentNumberingRequest Document() => new()
    {
        BillingDocumentId = 1,
        BillingDocumentType = BillingDocumentTypeARCAEnum.FA,
        BillingDocumentBookPrefix = 1,
        BillingDocumentDate = new DateTime(2026, 1, 15),
        Currency = "Pesos",
        ExchangeRate = 1,
        ConceptType = ConceptTypeARCAEnum.Products,
        AmountTax = 100,
        BillingDocumentNumberingTaxes = [new() { PercentageTax = "21.00", BaseAmount = 100, Amount = 21 }],
        Items = [new() { ItemDescription = "item", Amount = 121 }],
        IssuingCompany = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
        Client = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 30987654321 },
    };

    [Fact]
    public async Task Wsfe_operations_should_honour_a_cancelled_token()
    {
        var ops = new WsfeOperations(Options, NullLogger<WsfeOperations>.Instance);
        var ct = Cancelled();

        await FluentActions.Awaiting(() => ops.GetLastNumberAsync("s", "t", 20123456789, 1, 1, ct))
            .Should().ThrowAsync<OperationCanceledException>();
        await FluentActions.Awaiting(() => ops.SolicitarCaeAsync("s", "t", 20123456789, Document(), 1, ct))
            .Should().ThrowAsync<OperationCanceledException>();
        await FluentActions.Awaiting(() => ops.ConsultarComprobanteAsync("s", "t", 20123456789, 1, 1, 1, ct))
            .Should().ThrowAsync<OperationCanceledException>();
        await FluentActions.Awaiting(() => ops.GetPointsOfSaleAsync("s", "t", 20123456789, ct))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Wsfex_operations_should_honour_a_cancelled_token()
    {
        var ops = new WsfexOperations(Options, NullLogger<WsfexOperations>.Instance);
        var ct = Cancelled();

        await FluentActions.Awaiting(() => ops.GetLastNumberAsync("s", "t", 20123456789, 1, 1, ct))
            .Should().ThrowAsync<OperationCanceledException>();
        await FluentActions.Awaiting(() => ops.AuthorizeAsync("s", "t", 20123456789, Document(), 1, ct))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Padron_operations_should_honour_a_cancelled_token()
    {
        var ops = new PadronOperations(Options);

        await FluentActions.Awaiting(() => ops.GetPersonaAsync("s", "t", 20123456789, 30987654321, Cancelled()))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Invoice_verification_operations_should_honour_a_cancelled_token()
    {
        var ops = new InvoiceVerificationOperations(Options);
        var request = new InvoiceVerificationRequest
        {
            AuthorizationMode = AuthorizationModeARCAEnum.CAE,
            AuthorizationCode = "70000000000000",
            BillingDocumentType = BillingDocumentTypeARCAEnum.FA,
            BillingDocumentBookPrefix = 1,
            BillingDocumentNumber = 1,
            BillingDocumentDate = new DateTime(2026, 1, 15),
            TotalAmount = 121,
            IssuingCompany = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
        };

        await FluentActions.Awaiting(() => ops.VerifyAsync("s", "t", 20123456789, request, Cancelled()))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Electronic_mailbox_operations_should_honour_a_cancelled_token()
    {
        var ops = new ElectronicMailboxOperations(Options);
        var ct = Cancelled();

        await FluentActions.Awaiting(() => ops.ListAsync("s", "t", 20123456789, new MailboxQueryRequest(), ct))
            .Should().ThrowAsync<OperationCanceledException>();
        await FluentActions.Awaiting(() => ops.ConsumeAsync("s", "t", 20123456789, 1, ct))
            .Should().ThrowAsync<OperationCanceledException>();
    }
}
