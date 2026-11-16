// <copyright file="FileTokenCache.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Core;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

/// <summary>
/// File-based token cache. One encrypted binary file per service+company combination.
/// Payloads are protected with <see cref="IDataProtector"/>, which is cross-platform — the
/// previous DPAPI path only encrypted on Windows and wrote plain JSON everywhere else.
/// Uses per-key SemaphoreSlim to prevent concurrent read/write corruption.
/// Caché de tokens basada en archivos cifrados. Un archivo por combinación servicio+empresa.
/// El contenido se protege con <see cref="IDataProtector"/>, que es multiplataforma — el
/// camino anterior (DPAPI) solo cifraba en Windows y escribía JSON plano en el resto.
/// </summary>
internal sealed class FileTokenCache : ITokenCache
{
    // Buffer before considering a token expired (avoids clock edge cases).
    // Margen antes de considerar expirado el token (evita casos borde de reloj).
    private static readonly TimeSpan ExpirationBuffer = TimeSpan.FromMinutes(2);

    // Purpose string for the data protector — scopes the key so an unrelated protector in the
    // same application cannot read these payloads.
    // Purpose del protector — acota la clave para que otro protector de la misma aplicación no
    // pueda leer estos payloads.
    private const string ProtectorPurpose = "ElRoso.ARCA.TokenCache";

    private readonly string cacheDirectory;
    private readonly IDataProtector protector;
    private readonly ILogger<FileTokenCache> logger;

    // Per-key semaphores — ConcurrentDictionary avoids a global lock on registration.
    // Semáforos por clave — ConcurrentDictionary evita un lock global al registrar.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new();

    public FileTokenCache(string cacheDirectory, IDataProtectionProvider dataProtectionProvider, ILogger<FileTokenCache> logger)
    {
        this.cacheDirectory = cacheDirectory;
        this.protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        this.logger = logger;
        Directory.CreateDirectory(cacheDirectory);
    }

    public async Task<LoginTicketResponse?> GetAsync(string service, long companyId, CancellationToken ct = default)
    {
        var key = BuildKey(service, companyId);
        var path = BuildPath(key);
        var sem = GetSemaphore(key);

        await sem.WaitAsync(ct);
        try
        {
            LoginTicketResponse? ticket;
            try
            {
                // Async read — avoids blocking a thread-pool thread with sync I/O.
                // Lectura asíncrona — evita bloquear un hilo del pool con I/O síncrona.
                var data = await File.ReadAllBytesAsync(path, ct);
                ticket = DeserializeToken(data);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (CryptographicException)
            {
                // EN: Unreadable payload — written before encryption existed, or protected with a
                //     key this process no longer has. Either way it is a miss, never a parse.
                // ES: Payload ilegible — escrito antes de que existiera el cifrado, o protegido con
                //     una clave que este proceso ya no tiene. En ambos casos es un miss, no se parsea.
                logger.LogWarning("Token cache file could not be decrypted for {Service}/{CompanyId}. Discarding.", service, companyId);
                return null;
            }

            if (ticket == null)
                return null;

            // EN: ExpirationTime is a UTC instant (LoginTicketService normalizes it), so it is
            //     compared against UtcNow. The previous hardcoded UTC-3 only matched the writer
            //     while the container happened to run on Argentina time.
            // ES: ExpirationTime es un instante UTC (LoginTicketService lo normaliza), así que se
            //     compara contra UtcNow. El UTC-3 escrito a mano solo coincidía con el escritor
            //     mientras el contenedor corriera en hora argentina.
            if (ticket.ExpirationTime.Subtract(ExpirationBuffer) < DateTime.UtcNow)
            {
                logger.LogInformation("Token cache expired for {Service}/{CompanyId}.", service, companyId);
                return null;
            }

            logger.LogInformation("Token cache hit for {Service}/{CompanyId}.", service, companyId);
            return ticket;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read token cache for {Service}/{CompanyId}.", service, companyId);
            return null;
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task SetAsync(string service, long companyId, LoginTicketResponse ticket, CancellationToken ct = default)
    {
        var key = BuildKey(service, companyId);
        var path = BuildPath(key);
        var sem = GetSemaphore(key);

        await sem.WaitAsync(ct);
        try
        {
            await File.WriteAllBytesAsync(path, SerializeToken(ticket), ct);
            logger.LogInformation("Token cache written for {Service}/{CompanyId}.", service, companyId);
        }
        finally
        {
            sem.Release();
        }
    }

    private static string BuildKey(string service, long companyId) =>
        $"{service}_{companyId}";

    // EN: v2 — pre-v2 files stored the expiration in the host's local time and were not
    //     encrypted outside Windows. Versioning the name makes them invisible instead of
    //     readable with the wrong meaning.
    // ES: v2 — los archivos anteriores guardaban el vencimiento en hora local del host y no
    //     estaban cifrados fuera de Windows. Versionar el nombre los vuelve invisibles en vez de
    //     legibles con otro significado.
    private string BuildPath(string key) =>
        Path.Combine(cacheDirectory, $"ARCA_token_v2_{key}.bin");

    private SemaphoreSlim GetSemaphore(string key) =>
        locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

    /// <summary>
    /// Serializes and encrypts a token for disk storage.
    /// Serializa y cifra un token para almacenamiento en disco.
    /// </summary>
    private byte[] SerializeToken(LoginTicketResponse ticket)
    {
        var json = JsonConvert.SerializeObject(ticket);
        return protector.Protect(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Decrypts and deserializes a token from disk. Throws <see cref="CryptographicException"/>
    /// when the payload was not written by this protector — the caller treats that as a miss.
    /// Descifra y deserializa un token desde disco. Lanza <see cref="CryptographicException"/>
    /// si el payload no lo escribió este protector — el llamador lo trata como miss.
    /// </summary>
    private LoginTicketResponse? DeserializeToken(byte[] data)
    {
        var jsonBytes = protector.Unprotect(data);
        return JsonConvert.DeserializeObject<LoginTicketResponse>(Encoding.UTF8.GetString(jsonBytes));
    }
}
