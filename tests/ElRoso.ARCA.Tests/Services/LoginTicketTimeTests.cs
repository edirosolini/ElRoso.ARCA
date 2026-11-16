// EN: Tests for the time handling of the WSAA login ticket — parsing the expiration returned by
//     ARCA and building the request timestamps in Argentina time.
// ES: Tests del manejo de tiempo del login ticket de WSAA — parseo del vencimiento que devuelve
//     ARCA y armado de los timestamps del pedido en hora argentina.
using ElRoso.ARCA.Core;

namespace ElRoso.ARCA.Tests.Services;

public class LoginTicketTimeTests
{
    [Theory]
    [InlineData("2026-09-04T18:22:11.123-03:00")]
    [InlineData("2026-09-04T21:22:11.123Z")]
    [InlineData("2026-09-04T23:22:11.123+02:00")]
    public void ParseExpirationTime_should_return_the_same_instant_in_utc(string value)
    {
        // EN: Whatever offset ARCA writes, the parsed value is one and the same UTC instant —
        //     it must not depend on the container's TZ.
        // ES: Sea cual sea el offset que escriba ARCA, el valor parseado es el mismo instante en
        //     UTC — no puede depender del TZ del contenedor.
        var parsed = LoginTicketService.ParseExpirationTime(value);

        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
        Assert.Equal(new DateTime(2026, 9, 4, 21, 22, 11, 123, DateTimeKind.Utc), parsed);
    }

    [Fact]
    public void ParseExpirationTime_should_assume_argentina_time_when_the_offset_is_missing()
    {
        // EN: WSAA always sends an offset, but a value without one must not be read as UTC —
        //     that would push the ticket 3h into the future and keep an expired one alive.
        // ES: WSAA siempre manda offset, pero un valor sin offset no puede leerse como UTC — eso
        //     correria el ticket 3 h al futuro y mantendria vivo uno vencido.
        var parsed = LoginTicketService.ParseExpirationTime("2026-09-04T18:22:11");

        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
        Assert.Equal(new DateTime(2026, 9, 4, 21, 22, 11, DateTimeKind.Utc), parsed);
    }

    [Fact]
    public void ToArgentinaTime_should_apply_the_current_utc_minus_three_offset()
    {
        var utc = new DateTime(2026, 9, 4, 21, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 9, 4, 18, 0, 0), LoginTicketService.ToArgentinaTime(utc));
    }

    [Fact]
    public void ToArgentinaTime_should_follow_the_tz_database_when_argentina_was_on_dst()
    {
        // EN: Argentina ran DST (UTC-2) between Dec 2007 and Mar 2008. A hardcoded -3 gets this
        //     wrong; the tz database does not. Today's rule is not a guarantee about tomorrow's.
        // ES: Argentina uso horario de verano (UTC-2) entre dic 2007 y mar 2008. Un -3 escrito a
        //     mano se equivoca aca; la base de zonas horarias no. La regla de hoy no garantiza la
        //     de manana.
        var utc = new DateTime(2008, 1, 15, 21, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2008, 1, 15, 19, 0, 0), LoginTicketService.ToArgentinaTime(utc));
    }
}
