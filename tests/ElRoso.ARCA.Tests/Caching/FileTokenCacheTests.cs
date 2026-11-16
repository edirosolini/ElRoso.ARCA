// EN: Tests for FileTokenCache — encrypted file-based ticket cache.
// ES: Tests para FileTokenCache — caché de tickets en archivos cifrados.
using System.Text;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElRoso.ARCA.Tests.Caching;

public class FileTokenCacheTests : IDisposable
{
    private readonly string tempDir;
    private readonly FileTokenCache cache;

    private readonly IDataProtectionProvider dataProtection = new EphemeralDataProtectionProvider();

    public FileTokenCacheTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ElRoso.ARCA.Tests", Guid.NewGuid().ToString("N"));
        cache = new FileTokenCache(tempDir, dataProtection, NullLogger<FileTokenCache>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }

        GC.SuppressFinalize(this);
    }

    private static LoginTicketResponse FreshTicket(DateTime? expirationUtc = null) => new()
    {
        // EN: ExpirationTime is always a UTC instant — never the host's local time.
        // ES: ExpirationTime siempre es un instante UTC — nunca la hora local del host.
        ExpirationTime = expirationUtc ?? DateTime.UtcNow.AddHours(6),
        Sign = "fake-sign-value",
        Token = "fake-token-value",
    };

    [Fact]
    public void Constructor_should_create_cache_directory_if_missing()
    {
        var newDir = Path.Combine(Path.GetTempPath(), "ElRoso.ARCA.Tests", Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(newDir));

        _ = new FileTokenCache(newDir, dataProtection, NullLogger<FileTokenCache>.Instance);

        Assert.True(Directory.Exists(newDir));
        Directory.Delete(newDir, recursive: true);
    }

    [Fact]
    public async Task GetAsync_should_return_null_when_no_file_exists()
    {
        var result = await cache.GetAsync("wsfe", 20123456789, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_then_GetAsync_should_roundtrip_fresh_ticket()
    {
        var ticket = FreshTicket();

        await cache.SetAsync("wsfe", 20123456789, ticket, TestContext.Current.CancellationToken);
        var retrieved = await cache.GetAsync("wsfe", 20123456789, TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal(ticket.Token, retrieved!.Token);
        Assert.Equal(ticket.Sign, retrieved.Sign);
        Assert.Equal(ticket.ExpirationTime, retrieved.ExpirationTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SetAsync_should_write_file_with_expected_name()
    {
        await cache.SetAsync("wsfe", 20123456789, FreshTicket(), TestContext.Current.CancellationToken);

        // EN: v2 in the name — the previous format stored the expiration in the host's local
        //     time, so those files must be ignored instead of read with the wrong meaning.
        // ES: v2 en el nombre — el formato anterior guardaba el vencimiento en hora local del
        //     host, asi que esos archivos se ignoran en vez de leerse con otro significado.
        var expected = Path.Combine(tempDir, "ARCA_token_v2_wsfe_20123456789.bin");
        Assert.True(File.Exists(expected));
    }

    [Fact]
    public async Task GetAsync_should_return_null_for_expired_token()
    {
        // EN: ExpirationTime in the past — must be treated as miss (refresh required).
        //     One hour ago is inside the old UTC-3 window, so this also pins the UTC comparison.
        // ES: ExpirationTime en el pasado — debe tratarse como miss (hace falta refresh).
        //     Una hora atras cae dentro de la vieja ventana de UTC-3, asi que ademas fija la
        //     comparacion en UTC.
        var expired = FreshTicket(DateTime.UtcNow.AddHours(-1));

        await cache.SetAsync("wsfe", 20123456789, expired, TestContext.Current.CancellationToken);
        var result = await cache.GetAsync("wsfe", 20123456789, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_should_apply_expiration_buffer()
    {
        // EN: ExpirationBuffer = 2 min. Tickets expiring within that window must be treated as expired.
        // ES: ExpirationBuffer = 2 min. Tickets que expiran dentro de esa ventana son tratados como expirados.
        var ticket = FreshTicket(DateTime.UtcNow.AddSeconds(30)); // expires in 30s — within buffer

        await cache.SetAsync("wsfe", 20123456789, ticket, TestContext.Current.CancellationToken);
        var result = await cache.GetAsync("wsfe", 20123456789, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Different_service_and_company_should_have_isolated_caches()
    {
        var wsfeTicket = FreshTicket();
        wsfeTicket.Token = "wsfe-token";
        var padronTicket = FreshTicket();
        padronTicket.Token = "padron-token";

        await cache.SetAsync("wsfe", 20111111111, wsfeTicket, TestContext.Current.CancellationToken);
        await cache.SetAsync("ws_sr_constancia_inscripcion", 20111111111, padronTicket, TestContext.Current.CancellationToken);
        await cache.SetAsync("wsfe", 30222222222, wsfeTicket, TestContext.Current.CancellationToken);  // different company

        var wsfe1 = await cache.GetAsync("wsfe", 20111111111, TestContext.Current.CancellationToken);
        var padron1 = await cache.GetAsync("ws_sr_constancia_inscripcion", 20111111111, TestContext.Current.CancellationToken);
        var wsfe2 = await cache.GetAsync("wsfe", 30222222222, TestContext.Current.CancellationToken);

        Assert.Equal("wsfe-token", wsfe1!.Token);
        Assert.Equal("padron-token", padron1!.Token);
        Assert.NotNull(wsfe2);
    }

    [Fact]
    public async Task SetAsync_should_overwrite_existing_ticket()
    {
        var first = FreshTicket();
        first.Token = "first-token";
        var second = FreshTicket();
        second.Token = "second-token";

        await cache.SetAsync("wsfe", 20123456789, first, TestContext.Current.CancellationToken);
        await cache.SetAsync("wsfe", 20123456789, second, TestContext.Current.CancellationToken);
        var result = await cache.GetAsync("wsfe", 20123456789, TestContext.Current.CancellationToken);

        Assert.Equal("second-token", result!.Token);
    }

    [Fact]
    public async Task Concurrent_reads_on_same_key_should_not_throw()
    {
        // EN: SemaphoreSlim per-key must serialize without deadlock / corruption.
        // ES: SemaphoreSlim por clave debe serializar sin deadlock / corrupción.
        await cache.SetAsync("wsfe", 20123456789, FreshTicket(), TestContext.Current.CancellationToken);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => cache.GetAsync("wsfe", 20123456789))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.True(tasks.All(t => t.Result is not null));
    }

    [Fact]
    public async Task SetAsync_should_not_leave_token_or_sign_readable_on_disk()
    {
        // EN: The Token+Sign pair authorizes invoicing before ARCA for 12h — it must never sit
        //     in cleartext on disk, on any platform.
        // ES: El par Token+Sign autoriza a facturar ante ARCA por 12 h — nunca puede quedar en
        //     texto plano en disco, en ninguna plataforma.
        await cache.SetAsync("wsfe", 20123456789, FreshTicket(), TestContext.Current.CancellationToken);

        var file = Directory.GetFiles(tempDir).Single();
        var raw = await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken);
        var asText = Encoding.UTF8.GetString(raw);

        Assert.DoesNotContain("fake-token-value", asText);
        Assert.DoesNotContain("fake-sign-value", asText);
    }

    [Fact]
    public async Task GetAsync_should_discard_a_legacy_plaintext_cache_file()
    {
        // EN: Files written by the previous version are plain JSON. They must be treated as a
        //     miss (a new ticket is requested), never parsed.
        // ES: Los archivos de la versión anterior son JSON plano. Se tratan como miss (se pide
        //     un ticket nuevo), nunca se parsean.
        await cache.SetAsync("wsfe", 20123456789, FreshTicket(), TestContext.Current.CancellationToken);
        var file = Directory.GetFiles(tempDir).Single();
        await File.WriteAllTextAsync(file, "{\"Token\":\"legacy\",\"Sign\":\"legacy\",\"ExpirationTime\":\"2099-01-01T00:00:00\"}", TestContext.Current.CancellationToken);

        var result = await cache.GetAsync("wsfe", 20123456789, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_should_discard_a_file_protected_with_another_key()
    {
        // EN: A cache file that this process cannot decrypt is a miss, not a crash.
        // ES: Un archivo que este proceso no puede descifrar es un miss, no una excepción.
        await cache.SetAsync("wsfe", 20123456789, FreshTicket(), TestContext.Current.CancellationToken);

        var otherCache = new FileTokenCache(tempDir, new EphemeralDataProtectionProvider(), NullLogger<FileTokenCache>.Instance);
        var result = await otherCache.GetAsync("wsfe", 20123456789, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_should_ignore_a_cache_file_written_with_the_previous_name()
    {
        // EN: Pre-v2 files hold the expiration in the host's local time. Reading them would give
        //     the ticket a validity window that is off by the machine's UTC offset.
        // ES: Los archivos anteriores a v2 guardan el vencimiento en hora local del host. Leerlos
        //     le daria al ticket una ventana de validez corrida por el offset de la maquina.
        await cache.SetAsync("wsfe", 20123456789, FreshTicket(), TestContext.Current.CancellationToken);
        var current = Path.Combine(tempDir, "ARCA_token_v2_wsfe_20123456789.bin");
        File.Move(current, Path.Combine(tempDir, "ARCA_token_wsfe_20123456789.bin"));

        var result = await cache.GetAsync("wsfe", 20123456789, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_should_serve_a_ticket_that_is_valid_in_utc()
    {
        // EN: A ticket expiring in 10 minutes UTC is still usable. The previous reader compared
        //     against UtcNow-3h and would have served an already-dead ticket instead.
        // ES: Un ticket que vence en 10 minutos UTC todavia sirve. El lector anterior comparaba
        //     contra UtcNow-3h y habria servido uno ya muerto.
        await cache.SetAsync("wsfe", 20123456789, FreshTicket(DateTime.UtcNow.AddMinutes(10)), TestContext.Current.CancellationToken);

        var result = await cache.GetAsync("wsfe", 20123456789, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
    }
}
