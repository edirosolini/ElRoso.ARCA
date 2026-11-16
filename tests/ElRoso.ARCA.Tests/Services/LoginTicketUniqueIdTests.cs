// EN: Tests for the WSAA uniqueId generator — it must keep growing across process restarts.
// ES: Tests del generador de uniqueId del WSAA — tiene que seguir creciendo entre reinicios.
using ElRoso.ARCA.Core;

namespace ElRoso.ARCA.Tests.Services;

public class LoginTicketUniqueIdTests
{
    [Fact]
    public void ComputeUniqueId_should_derive_from_the_clock_on_a_cold_start()
    {
        // EN: A process-local counter restarted at 1 on every deploy. Seeding from unix seconds
        //     makes the first id after a restart pick up where the previous process left off.
        // ES: Un contador en memoria volvia a 1 en cada deploy. Sembrar desde los segundos unix
        //     hace que el primer id despues de un reinicio siga donde quedo el proceso anterior.
        Assert.Equal(1_800_000_000u, LoginTicketService.ComputeUniqueId(previous: 0, unixSeconds: 1_800_000_000));
    }

    [Fact]
    public void ComputeUniqueId_should_advance_within_the_same_second()
    {
        Assert.Equal(1_800_000_001u, LoginTicketService.ComputeUniqueId(previous: 1_800_000_000, unixSeconds: 1_800_000_000));
    }

    [Fact]
    public void ComputeUniqueId_should_not_go_backwards_when_the_clock_does()
    {
        // EN: An NTP correction or a host clock jump must never reissue an id already sent.
        // ES: Una correccion de NTP o un salto del reloj del host nunca puede reemitir un id ya
        //     mandado.
        Assert.Equal(1_800_000_006u, LoginTicketService.ComputeUniqueId(previous: 1_800_000_005, unixSeconds: 1_700_000_000));
    }

    [Fact]
    public void NextUniqueId_should_be_strictly_increasing_and_clock_based()
    {
        var first = LoginTicketService.NextUniqueId();
        var second = LoginTicketService.NextUniqueId();

        Assert.True(second > first);

        // EN: Well past any in-memory counter — proves the id is not restarting at 1.
        // ES: Muy por encima de cualquier contador en memoria — prueba que el id no reinicia en 1.
        Assert.True(first > 1_700_000_000);
    }

    [Fact]
    public void NextUniqueId_should_not_repeat_under_concurrency()
    {
        var ids = new uint[500];

        Parallel.For(0, ids.Length, i => ids[i] = LoginTicketService.NextUniqueId());

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }
}
