// EN: Tests for SoapInvoker — bridges a CancellationToken into the generated SOAP proxies (which
//     take none) and guarantees the WCF channel is always closed or aborted.
// ES: Tests de SoapInvoker — puentea un CancellationToken hacia los proxies SOAP generados (que no
//     lo reciben) y garantiza que el canal WCF siempre se cierre o se aborte.
using System.ServiceModel;
using ElRoso.ARCA.Core;

namespace ElRoso.ARCA.Tests.Soap;

public class SoapInvokerTests
{
    [Fact]
    public async Task InvokeAsync_should_return_the_result_and_close_the_channel()
    {
        var client = new Mock<ICommunicationObject>();

        var result = await SoapInvoker.InvokeAsync(client.Object, () => Task.FromResult(42), CancellationToken.None);

        Assert.Equal(42, result);
        client.Verify(c => c.Close(), Times.Once);
        client.Verify(c => c.Abort(), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_should_abort_the_channel_and_stop_waiting_when_cancelled()
    {
        // EN: The generated proxies never observe the token, so a caller that walks away used to
        //     keep waiting for ARCA. Cancelling must tear the channel down and return control.
        // ES: Los proxies generados nunca miran el token, asi que un llamador que se iba seguia
        //     esperando a ARCA. Cancelar tiene que bajar el canal y devolver el control.
        var client = new Mock<ICommunicationObject>();
        using var cts = new CancellationTokenSource();
        var neverCompletes = new TaskCompletionSource<int>();

        var invocation = SoapInvoker.InvokeAsync(client.Object, () => neverCompletes.Task, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);
        client.Verify(c => c.Abort(), Times.Once);
        client.Verify(c => c.Close(), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_should_not_leak_the_channel_when_the_call_fails()
    {
        var client = new Mock<ICommunicationObject>();

        await Assert.ThrowsAnyAsync<TimeoutException>(
            () => SoapInvoker.InvokeAsync<int>(
                client.Object,
                () => Task.FromException<int>(new TimeoutException("ARCA took too long")),
                CancellationToken.None));

        client.Verify(c => c.Abort(), Times.Once);
        client.Verify(c => c.Close(), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_should_abort_when_closing_the_channel_fails()
    {
        // EN: A faulted channel throws on Close(). The result is already in hand, so the call
        //     must succeed and the channel must be aborted instead of leaked.
        // ES: Un canal en falla tira excepcion en Close(). El resultado ya esta, asi que la
        //     llamada tiene que salir bien y el canal se aborta en vez de quedar colgado.
        var client = new Mock<ICommunicationObject>();
        client.Setup(c => c.Close()).Throws(new CommunicationException("channel faulted"));

        var result = await SoapInvoker.InvokeAsync(client.Object, () => Task.FromResult("cae"), CancellationToken.None);

        Assert.Equal("cae", result);
        client.Verify(c => c.Abort(), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_should_fail_fast_when_the_token_is_already_cancelled()
    {
        var client = new Mock<ICommunicationObject>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SoapInvoker.InvokeAsync(client.Object, () => new TaskCompletionSource<int>().Task, cts.Token));

        client.Verify(c => c.Abort(), Times.Once);
    }
}
