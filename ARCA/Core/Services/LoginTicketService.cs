// <copyright file="LoginTicketService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Core;

using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Xml;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;
using Microsoft.Extensions.Logging;

internal sealed class LoginTicketService : ILoginTicketService
{
    // EN: WSAA reasons in Argentina wall-clock time. Resolving the zone by name keeps a future
    //     DST change from silently shifting every timestamp — a hardcoded -3 could not.
    // ES: WSAA razona en hora de pared argentina. Resolver la zona por nombre evita que un cambio
    //     futuro de horario de verano corra todos los timestamps en silencio — un -3 escrito a
    //     mano no puede.
    private static readonly TimeZoneInfo ArgentinaTimeZone = ResolveArgentinaTimeZone();

    // EN: Last uniqueId handed out. Seeded from the clock rather than from 0 so a redeploy does
    //     not rewind the sequence WSAA expects to keep growing per (CUIT, service).
    // ES: Último uniqueId entregado. Se siembra del reloj y no de 0, así un redeploy no rebobina
    //     la secuencia que WSAA espera creciente por (CUIT, servicio).
    private static long lastUniqueId;

    // NTP response cache — avoids a UDP round-trip on every token refresh.
    // WSAA accepts timestamps within ±10 minutes of server time.
    // Caché de respuesta NTP — evita un round-trip UDP en cada renovación de token.
    // WSAA acepta timestamps dentro de ±10 minutos de la hora del servidor.
    private static DateTime s_cachedNtpTime;
    private static long s_cacheTimestampMs;
    private static readonly object s_ntpLock = new();
    private const long NtpCacheMs = 60_000;

    private readonly ILogger<LoginTicketService> logger;
    private readonly ICertificateService certificateService;

    private const string XmlTemplate =
        "<loginTicketRequest>" +
          "<header>" +
            "<uniqueId></uniqueId>" +
            "<generationTime></generationTime>" +
            "<expirationTime></expirationTime>" +
          "</header>" +
          "<service></service>" +
        "</loginTicketRequest>";

    public LoginTicketService(ILogger<LoginTicketService> logger, ICertificateService certificateService)
    {
        this.logger = logger;
        this.certificateService = certificateService;
    }

    public async Task<LoginTicketResponse> GetLoginTicketAsync(
        string service,
        string wsaaUrl,
        string certificatePath,
        string? certificatePassword,
        CancellationToken ct = default)
    {
        // PASO 1: Build the LoginTicketRequest XML.
        // PASO 1: Construir el XML del LoginTicketRequest.
        string signedBase64;
        try
        {
            var ARCATime = ToArgentinaTime(await GetNetworkTimeAsync(ct));
            var uniqueId = NextUniqueId();

            var doc = new XmlDocument();
            doc.LoadXml(XmlTemplate);

            doc.SelectSingleNode("//uniqueId")!.InnerText = uniqueId.ToString();
            doc.SelectSingleNode("//generationTime")!.InnerText = ARCATime.AddMinutes(-2).ToString("s");
            doc.SelectSingleNode("//expirationTime")!.InnerText = ARCATime.AddMinutes(+10).ToString("s");
            doc.SelectSingleNode("//service")!.InnerText = service;

            logger.LogInformation("LoginTicketRequest built for service '{Service}', uniqueId={UniqueId}.", service, uniqueId);

            // PASO 2: Sign with PKCS#7 using the X.509 certificate.
            // PASO 2: Firmar con PKCS#7 usando el certificado X.509.
            var cert = certificateService.LoadCertificate(certificatePath, certificatePassword);
            var msgBytes = Encoding.UTF8.GetBytes(doc.OuterXml);
            var signed = certificateService.SignMessage(msgBytes, cert);
            signedBase64 = Convert.ToBase64String(signed);
        }
        catch (ARCAAuthException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ARCAAuthException("Failed to build or sign the LoginTicketRequest.", ex);
        }

        // PASO 3: Call WSAA to get the Login Ticket.
        // PASO 3: Llamar al WSAA para obtener el Login Ticket.
        string ticketXml;
        try
        {
            logger.LogInformation("Calling WSAA at {Url}.", wsaaUrl);
            var client = new WSAA.LoginCMSClient(WSAA.LoginCMSClient.EndpointConfiguration.LoginCms, wsaaUrl);
            var response = await SoapInvoker.InvokeAsync(
                client,
                () => client.loginCmsAsync(new WSAA.loginCmsRequest
                {
                    Body = new WSAA.loginCmsRequestBody { in0 = signedBase64 },
                }),
                ct);
            ticketXml = response.Body.loginCmsReturn;
            logger.LogInformation("WSAA responded successfully.");
        }
        catch (ARCAAuthException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ARCAAuthException("WSAA call failed.", ex);
        }

        // PASO 4: Parse the Login Ticket Response XML.
        // PASO 4: Parsear el XML de respuesta del WSAA.
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(ticketXml);

            return new LoginTicketResponse
            {
                Token = doc.SelectSingleNode("//token")!.InnerText,
                Sign = doc.SelectSingleNode("//sign")!.InnerText,

                ExpirationTime = ParseExpirationTime(doc.SelectSingleNode("//expirationTime")!.InnerText),
            };
        }
        catch (Exception ex)
        {
            throw new ARCAAuthException("Failed to parse the WSAA LoginTicketResponse XML.", ex);
        }
    }

    /// <summary>
    /// EN: Returns the next WSAA uniqueId. A process-local counter restarted at 1 on every deploy,
    ///     which can trip the anti-replay control on WSAA's side; deriving it from unix seconds
    ///     keeps the sequence monotonic across restarts without any persisted state.
    /// ES: Devuelve el próximo uniqueId del WSAA. Un contador en memoria reiniciaba en 1 en cada
    ///     deploy, lo que puede activar el control anti-replay del lado de WSAA; derivarlo de los
    ///     segundos unix mantiene la secuencia monótona entre reinicios sin persistir nada.
    /// </summary>
    internal static uint NextUniqueId()
    {
        var unixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        while (true)
        {
            var previous = Interlocked.Read(ref lastUniqueId);
            var next = ComputeUniqueId(previous, unixSeconds);
            if (Interlocked.CompareExchange(ref lastUniqueId, next, previous) == previous)
                return (uint)next;
        }
    }

    /// <summary>
    /// EN: The clock seeds the value; <paramref name="previous"/> guarantees it never repeats or
    ///     goes backwards when several tickets are requested inside the same second, or when the
    ///     host clock jumps back (NTP correction).
    /// ES: El reloj siembra el valor; <paramref name="previous"/> garantiza que no se repita ni
    ///     retroceda cuando se piden varios tickets en el mismo segundo, o cuando el reloj del
    ///     host salta hacia atrás (corrección de NTP).
    /// </summary>
    internal static long ComputeUniqueId(long previous, long unixSeconds) =>
        Math.Max(unixSeconds, previous + 1);

    /// <summary>
    /// EN: Parses the expirationTime returned by WSAA into a UTC instant. Plain
    ///     <c>DateTime.Parse</c> folds the offset into the machine's local time and marks the
    ///     result as <c>Local</c>, so the ticket's validity ends up depending on the container's
    ///     TZ. A value without an offset is read as Argentina time, which is what WSAA sends.
    /// ES: Parsea el expirationTime que devuelve WSAA como instante UTC. Un
    ///     <c>DateTime.Parse</c> pelado pliega el offset a la hora local de la máquina y marca el
    ///     resultado como <c>Local</c>, así que la validez del ticket termina dependiendo del TZ
    ///     del contenedor. Un valor sin offset se lee como hora argentina, que es lo que manda WSAA.
    /// </summary>
    internal static DateTime ParseExpirationTime(string value)
    {
        // RoundtripKind keeps what the text said: 'Z' -> Utc, an explicit offset -> Local
        // (already converted to this machine's clock), nothing -> Unspecified.
        // RoundtripKind conserva lo que decía el texto: 'Z' -> Utc, un offset explícito -> Local
        // (ya convertido al reloj de esta máquina), nada -> Unspecified.
        var parsed = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        return parsed.Kind switch
        {
            DateTimeKind.Utc => parsed,
            DateTimeKind.Local => parsed.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(parsed, ArgentinaTimeZone),
        };
    }

    /// <summary>
    /// EN: Converts a UTC instant to Argentina wall-clock time using the tz database.
    /// ES: Convierte un instante UTC a hora de pared argentina usando la base de zonas horarias.
    /// </summary>
    internal static DateTime ToArgentinaTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ArgentinaTimeZone);

    /// <summary>
    /// EN: Falls back to a fixed UTC-3 zone when the host has no tz database (trimmed containers,
    ///     invariant globalization) — losing DST accuracy beats failing to authenticate.
    /// ES: Cae a una zona fija UTC-3 cuando el host no tiene base de zonas horarias (contenedores
    ///     recortados, globalización invariante) — perder la precisión de DST es mejor que no
    ///     poder autenticar.
    /// </summary>
    private static TimeZoneInfo ResolveArgentinaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "ARCA-Argentina",
                TimeSpan.FromHours(-3),
                "Argentina (fallback)",
                "Argentina (fallback)");
        }
    }

    /// <summary>
    /// Fetches current time from ARCA's NTP server, cached for 60 s.
    /// Falls back to system clock if NTP is unreachable — WSAA tolerates ±10 min.
    /// Obtiene la hora del servidor NTP de ARCA, cacheada por 60 s.
    /// Usa el reloj del sistema como fallback — WSAA tolera ±10 min de desvío.
    /// </summary>
    private static async Task<DateTime> GetNetworkTimeAsync(CancellationToken ct)
    {
        long nowMs = Environment.TickCount64;
        lock (s_ntpLock)
        {
            if (s_cachedNtpTime != default && nowMs - s_cacheTimestampMs < NtpCacheMs)
                return s_cachedNtpTime;
        }

        const string ntpServer = "time.afip.gov.ar";
        const int ntpPort = 123;
        const int ntpDataLength = 48;

        DateTime result;
        try
        {
            var ntpData = new byte[ntpDataLength];
            ntpData[0] = 0x1B; // NTP LI=0, VN=3, Mode=3 (client)

            // Hard 5-second timeout so a slow/unresponsive NTP server doesn't block auth.
            // Timeout duro de 5 segundos para que un NTP lento/caído no bloquee la autenticación.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var udp = new UdpClient();
            await udp.SendAsync(ntpData, ntpDataLength, ntpServer, ntpPort).WaitAsync(cts.Token);
            var received = await udp.ReceiveAsync(cts.Token);
            var data = received.Buffer;

            if (data.Length < ntpDataLength)
                throw new InvalidOperationException($"NTP response too short: {data.Length} bytes.");

            ulong intPart = ((ulong)data[40] << 24) | ((ulong)data[41] << 16) | ((ulong)data[42] << 8) | data[43];
            ulong fracPart = ((ulong)data[44] << 24) | ((ulong)data[45] << 16) | ((ulong)data[46] << 8) | data[47];
            var ms = (intPart * 1000) + (fracPart * 1000 / 0x100000000L);
            result = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((long)ms);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // NTP timeout — fall back to system clock.
            // Timeout de NTP — se usa el reloj del sistema.
            result = DateTime.UtcNow;
        }
        catch
        {
            // Any NTP failure — fall back to system clock.
            // Cualquier falla de NTP — se usa el reloj del sistema.
            result = DateTime.UtcNow;
        }

        lock (s_ntpLock)
        {
            s_cachedNtpTime = result;
            s_cacheTimestampMs = Environment.TickCount64;
        }

        return result;
    }
}
