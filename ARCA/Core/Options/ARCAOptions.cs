// <copyright file="ARCAOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Core;

/// <summary>
/// Configuration options for the ARCA (ex-AFIP) client library.
/// Opciones de configuración para la librería cliente de ARCA (ex-AFIP).
/// </summary>
public class ARCAOptions
{
    /// <summary>
    /// Use production endpoints. Default: false (homologation/testing).
    /// Usar endpoints de producción. Por defecto: false (homologación).
    /// </summary>
    public bool IsProduction { get; set; } = false;

    /// <summary>
    /// Absolute path to the X.509 certificate file (.pfx or .p12).
    /// Ruta absoluta al archivo de certificado X.509 (.pfx o .p12).
    /// </summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// Certificate password. Null if the certificate has no password.
    /// Contraseña del certificado. Null si no tiene contraseña.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Directory where login ticket cache files are stored.
    /// Defaults to the system temp folder.
    /// Directorio donde se almacenan los archivos de caché de tokens.
    /// </summary>
    public string TokenCacheDirectory { get; set; } = Path.GetTempPath();

    /// <summary>
    /// SOAP timeout in seconds. Default: 30.
    /// Timeout de las llamadas SOAP en segundos.
    /// </summary>
    public int SoapTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Si la emisión consulta el Padrón A5 para el nombre y la condición de un receptor con CUIT; false usa la del consumidor.
    /// </summary>
    public bool ResolveReceiverFromPadron { get; set; } = true;

    /// <summary>
    /// EN: WSCComu (e-Ventanilla / DFE) endpoint URL. By default it is resolved automatically from
    /// <see cref="IsProduction"/> — same convention used by every other ARCA WS in this lib. Override
    /// it only if ARCA migrates the URL or if you proxy through your own middleware.
    /// ES: URL del WSCComu. Por defecto se resuelve automáticamente desde <see cref="IsProduction"/>
    /// — la misma convención que usan el resto de los WS. Solo seteala manualmente si ARCA migra la
    /// URL o si proxyás a través de un middleware propio.
    /// </summary>
    public string WsccomuUrl
    {
        get => string.IsNullOrWhiteSpace(this.wscomuUrlOverride)
            ? (this.IsProduction
                ? "https://infraestructura.afip.gob.ar/ve-ws/services/veconsumer"
                : "https://stable-middleware-tecno-ext.afip.gob.ar/ve-ws/services/veconsumer")
            : this.wscomuUrlOverride;
        set => this.wscomuUrlOverride = value;
    }

    private string? wscomuUrlOverride;

    // --- Internal resolved URLs (set by SetIsProduction) ---

    internal string WsaaUrl => IsProduction
        ? "https://wsaa.afip.gov.ar/ws/services/LoginCms"
        : "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";

    internal string WsfeUrl => IsProduction
        ? "https://servicios1.afip.gov.ar/wsfev1/service.asmx"
        : "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";

    internal string WsfexUrl => IsProduction
        ? "https://servicios1.afip.gov.ar/WSFEXv1/service.asmx"
        : "https://wswhomo.afip.gov.ar/WSFEXv1/service.asmx";

    internal string PadronUrl => IsProduction
        ? "https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA5"
        : "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5";

    internal string WscdcUrl => IsProduction
        ? "https://servicios1.afip.gov.ar/WSCDC/service.asmx"
        : "https://wswhomo.afip.gov.ar/WSCDC/service.asmx";
}
