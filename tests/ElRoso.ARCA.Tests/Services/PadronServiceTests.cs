// PadronService: ticket de ws_sr_constancia_inscripcion, CUIT representado, CUIT inexistente y cancelación.
using System.ServiceModel;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElRoso.ARCA.Tests.Services;

public class PadronServiceTests
{
    private const long RepresentedCuit = 20123456789;
    private const long QueriedCuit = 30987654321;

    private readonly Mock<ILoginTicketService> loginTicketServiceMock = new();
    private readonly Mock<ITokenCache> tokenCacheMock = new();
    private readonly Mock<IPadronOperations> padronMock = new();
    private readonly ARCAOptions options = new()
    {
        IsProduction = false,
        CertificatePath = "/dev/null",
        TokenCacheDirectory = Path.GetTempPath(),
        SoapTimeoutSeconds = 5,
    };

    private static LoginTicketResponse Ticket() => new()
    {
        Token = "fake-token",
        Sign = "fake-sign",
        ExpirationTime = DateTime.UtcNow.AddHours(6),
    };

    private PadronService CreateService() => new(
        NullLogger<PadronService>.Instance,
        loginTicketServiceMock.Object,
        tokenCacheMock.Object,
        options,
        padronMock.Object);

    private void SetupTokenCacheHit() =>
        tokenCacheMock
            .Setup(t => t.GetAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ticket());

    [Fact]
    public async Task GetPersonaAsync_should_use_the_constancia_ticket_of_the_represented_cuit()
    {
        SetupTokenCacheHit();
        var persona = new PadronPersonaResponse { Found = true, Cuit = QueriedCuit, DisplayName = "EMPRESA DEMO SA" };
        padronMock
            .Setup(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PadronPersonaResult { Persona = persona });

        var result = await CreateService().GetPersonaAsync(RepresentedCuit, QueriedCuit);

        result.Should().BeSameAs(persona);
        tokenCacheMock.Verify(t => t.GetAsync("ws_sr_constancia_inscripcion", RepresentedCuit, It.IsAny<CancellationToken>()), Times.Once);
        padronMock.Verify(p => p.GetPersonaAsync("fake-sign", "fake-token", RepresentedCuit, QueriedCuit, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPersonaAsync_should_request_and_cache_a_ticket_on_cache_miss()
    {
        var ticket = Ticket();
        loginTicketServiceMock
            .Setup(l => l.GetLoginTicketAsync("ws_sr_constancia_inscripcion", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        padronMock
            .Setup(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PadronPersonaResult { Persona = new PadronPersonaResponse { Found = true } });

        await CreateService().GetPersonaAsync(RepresentedCuit, QueriedCuit);

        tokenCacheMock.Verify(t => t.SetAsync("ws_sr_constancia_inscripcion", RepresentedCuit, ticket, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPersonaAsync_should_return_not_found_when_ARCA_does_not_know_the_cuit()
    {
        SetupTokenCacheHit();
        padronMock
            .Setup(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ARCAServiceException("ARCA Padron A5 getPersona failed.", new FaultException("No existe persona con ese Id")));

        var result = await CreateService().GetPersonaAsync(RepresentedCuit, QueriedCuit);

        result.Found.Should().BeFalse();
        result.Cuit.Should().Be(QueriedCuit);
        result.Errors.Should().ContainSingle().Which.Should().Be("No existe persona con ese Id");
    }

    [Fact]
    public async Task GetPersonaAsync_should_propagate_other_ARCA_failures()
    {
        SetupTokenCacheHit();
        padronMock
            .Setup(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ARCAServiceException("ARCA Padron A5 getPersona failed.", new TimeoutException()));

        var act = () => CreateService().GetPersonaAsync(RepresentedCuit, QueriedCuit);

        await act.Should().ThrowAsync<ARCAServiceException>();
    }

    [Fact]
    public async Task GetPersonaAsync_should_honour_a_cancelled_token_before_touching_the_network()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => CreateService().GetPersonaAsync(RepresentedCuit, QueriedCuit, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        tokenCacheMock.Verify(t => t.GetAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        padronMock.Verify(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPersonaAsync_should_propagate_cancellation_from_the_SOAP_call()
    {
        SetupTokenCacheHit();
        padronMock
            .Setup(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => CreateService().GetPersonaAsync(RepresentedCuit, QueriedCuit);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
