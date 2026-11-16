// EN: Tests for ElectronicMailboxService — orchestrates WSAA + WSCComu (e-Ventanilla).
// ES: Tests para ElectronicMailboxService — orquesta WSAA + WSCComu (e-Ventanilla).
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElRoso.ARCA.Tests.Services;

public class ElectronicMailboxServiceTests
{
    private readonly Mock<ILoginTicketService> loginTicketServiceMock = new();
    private readonly Mock<ITokenCache> tokenCacheMock = new();
    private readonly Mock<IElectronicMailboxOperations> operationsMock = new();
    private readonly ARCAOptions options = new()
    {
        IsProduction = false,
        CertificatePath = "/dev/null",
        TokenCacheDirectory = Path.GetTempPath(),
        SoapTimeoutSeconds = 5,
    };

    private ElectronicMailboxService CreateService() => new(
        NullLogger<ElectronicMailboxService>.Instance,
        loginTicketServiceMock.Object,
        tokenCacheMock.Object,
        options,
        operationsMock.Object);

    private static IssuingCompanyRequest ValidIssuer() => new()
    {
        DocumentType = DocumentTypeARCAEnum.CUIT,
        DocumentNumber = 20123456789,
    };

    private static LoginTicketResponse FreshTicket() => new()
    {
        Token = "fake-token",
        Sign = "fake-sign",
        ExpirationTime = DateTime.UtcNow.AddHours(-3).AddHours(6),
    };

    [Fact]
    public async Task ListAsync_should_throw_when_IssuingCompany_DocumentNumber_is_zero()
    {
        var service = CreateService();

        var act = () => service.ListAsync(new MailboxQueryRequest { IssuingCompany = new IssuingCompanyRequest() });

        await Assert.ThrowsAsync<ARCAValidationException>(act);
    }

    [Fact]
    public async Task ListAsync_with_cache_hit_should_use_cached_ticket_and_call_operations()
    {
        tokenCacheMock
            .Setup(t => t.GetAsync("veconsumerws", It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshTicket());

        operationsMock
            .Setup(o => o.ListAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                    It.IsAny<MailboxQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailboxQueryResponse
            {
                Page = 1,
                TotalPages = 1,
                ItemsPerPage = 50,
                TotalItems = 2,
                Messages =
                [
                    new MailboxMessageSummary { Id = 100, Subject = "Notificación de vencimiento", StateName = "Nueva" },
                    new MailboxMessageSummary { Id = 101, Subject = "Recategorización Monotributo", StateName = "Leída" },
                ],
            });

        var service = CreateService();
        var response = await service.ListAsync(new MailboxQueryRequest { IssuingCompany = ValidIssuer() }, TestContext.Current.CancellationToken);

        Assert.Equal(2, response.Messages.Count());
        Assert.Equal("Notificación de vencimiento", response.Messages[0].Subject);
        Assert.Equal(2, response.TotalItems);

        loginTicketServiceMock.VerifyNoOtherCalls();
        tokenCacheMock.Verify(t => t.GetAsync("veconsumerws", It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_on_cache_miss_should_refresh_via_WSAA()
    {
        tokenCacheMock
            .Setup(t => t.GetAsync("veconsumerws", It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginTicketResponse?)null);

        loginTicketServiceMock
            .Setup(l => l.GetLoginTicketAsync("veconsumerws", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshTicket());

        operationsMock
            .Setup(o => o.ListAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                    It.IsAny<MailboxQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailboxQueryResponse());

        var service = CreateService();
        await service.ListAsync(new MailboxQueryRequest { IssuingCompany = ValidIssuer() }, TestContext.Current.CancellationToken);

        loginTicketServiceMock.Verify(l => l.GetLoginTicketAsync(
            "veconsumerws", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        tokenCacheMock.Verify(t => t.SetAsync(
            "veconsumerws", It.IsAny<long>(), It.IsAny<LoginTicketResponse>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_should_throw_when_DocumentNumber_is_zero()
    {
        var service = CreateService();

        var act = () => service.ConsumeAsync(new MailboxConsumeRequest
        {
            IssuingCompany = new IssuingCompanyRequest(),
            MessageId = 100,
        });

        await Assert.ThrowsAsync<ARCAValidationException>(act);
    }

    [Fact]
    public async Task ConsumeAsync_should_throw_when_MessageId_is_zero()
    {
        var service = CreateService();

        var act = () => service.ConsumeAsync(new MailboxConsumeRequest
        {
            IssuingCompany = ValidIssuer(),
            MessageId = 0,
        });

        await Assert.ThrowsAsync<ARCAValidationException>(act);
    }

    [Fact]
    public async Task ConsumeAsync_with_valid_request_returns_full_message()
    {
        tokenCacheMock
            .Setup(t => t.GetAsync("veconsumerws", It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshTicket());

        operationsMock
            .Setup(o => o.ConsumeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                       100L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailboxConsumeResponse
            {
                Success = true,
                Message = new MailboxMessage
                {
                    Id = 100,
                    Subject = "Notificación importante",
                    Body = "<p>Estimado contribuyente...</p>",
                    HasAttachment = false,
                },
            });

        var service = CreateService();
        var response = await service.ConsumeAsync(new MailboxConsumeRequest
        {
            IssuingCompany = ValidIssuer(),
            MessageId = 100,
        }, TestContext.Current.CancellationToken);

        Assert.True(response.Success);
        Assert.NotNull(response.Message);
        Assert.Equal("Notificación importante", response.Message!.Subject);
        Assert.Contains("Estimado contribuyente", response.Message.Body);
    }

    [Fact]
    public async Task ConsumeAsync_with_unknown_message_should_return_error_response()
    {
        tokenCacheMock
            .Setup(t => t.GetAsync("veconsumerws", It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshTicket());

        operationsMock
            .Setup(o => o.ConsumeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                       It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailboxConsumeResponse
            {
                Success = false,
                Errors = ["WSCComu returned no message for the given id."],
            });

        var service = CreateService();
        var response = await service.ConsumeAsync(new MailboxConsumeRequest
        {
            IssuingCompany = ValidIssuer(),
            MessageId = 999999,
        }, TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.NotEmpty(response.Errors);
        Assert.Null(response.Message);
    }

    [Fact]
    public async Task ListAsync_should_propagate_pagination_metadata()
    {
        tokenCacheMock
            .Setup(t => t.GetAsync("veconsumerws", It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshTicket());

        operationsMock
            .Setup(o => o.ListAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                    It.IsAny<MailboxQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MailboxQueryResponse
            {
                Page = 3,
                TotalPages = 10,
                ItemsPerPage = 20,
                TotalItems = 195,
                Messages = [],
            });

        var service = CreateService();
        var response = await service.ListAsync(new MailboxQueryRequest
        {
            IssuingCompany = ValidIssuer(),
            Page = 3,
            PageSize = 20,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(3, response.Page);
        Assert.Equal(10, response.TotalPages);
        Assert.Equal(20, response.ItemsPerPage);
        Assert.Equal(195, response.TotalItems);
    }
}