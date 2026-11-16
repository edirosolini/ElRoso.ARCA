// EN: Minimal sample — emits a Factura A in ARCA homologation and prints the CAE + QR URL.
// ES: Sample mínimo — emite una Factura A en homologación ARCA e imprime el CAE + URL del QR.
using ElRoso.ARCA;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// --- Configuration ---
// EN: Reads from appsettings.json + environment variables. See appsettings.json.example.
// ES: Lee de appsettings.json + variables de entorno. Ver appsettings.json.example.
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "ARCA_")
    .Build();

var arca = config.GetSection("Arca");
var issuerCuit = long.Parse(config["Sample:IssuerCuit"]
    ?? throw new InvalidOperationException("Missing Sample:IssuerCuit"));
var clientCuit = long.Parse(config["Sample:ClientCuit"]
    ?? throw new InvalidOperationException("Missing Sample:ClientCuit"));

// --- DI ---
var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));
services.AddARCAClient(options =>
{
    options.IsProduction = arca.GetValue<bool>("IsProduction");
    options.CertificatePath = arca["CertificatePath"]!;
    options.CertificatePassword = arca["CertificatePassword"];
    options.TokenCacheDirectory = arca["TokenCacheDirectory"] ?? Path.GetTempPath();
    options.SoapTimeoutSeconds = arca.GetValue<int>("SoapTimeoutSeconds", 30);
});

await using var sp = services.BuildServiceProvider();
var logger = sp.GetRequiredService<ILogger<Program>>();
var billing = sp.GetRequiredService<IBillingDocumentNumberingService>();

logger.LogInformation("Environment: {Env}", arca.GetValue<bool>("IsProduction") ? "PRODUCTION" : "Homologation");
logger.LogInformation("Issuer CUIT: {Cuit}", issuerCuit);

// --- Build the request ---
var request = new BillingDocumentNumberingRequest
{
    BillingDocumentType = BillingDocumentTypeARCAEnum.FA,
    BillingDocumentBookPrefix = 1,                               // Point of sale (Punto de venta)
    BillingDocumentDate = DateTime.Today,
    Currency = "Pesos",
    ExchangeRate = 1,
    ConceptType = ConceptTypeARCAEnum.Products,
    AmountTax = 1000.00,
    BillingDocumentNumberingTaxes =
    [
        new() { PercentageTax = "21.00", BaseAmount = 1000.00, Amount = 210.00 }
    ],
    IssuingCompany = new()
    {
        DocumentType = DocumentTypeARCAEnum.CUIT,
        DocumentNumber = issuerCuit,
    },
    Client = new()
    {
        DocumentType = DocumentTypeARCAEnum.CUIT,
        DocumentNumber = clientCuit,
    },
};

// --- Invoke ARCA ---
try
{
    var response = await billing.AuthorizeAsync(request);

    if (response.Result)
    {
        logger.LogInformation("✅ CAE: {CAE}", response.CAE);
        logger.LogInformation("Document number: {Number}", response.BillingDocumentNumber);
        logger.LogInformation("CAE expires: {Expiration:yyyy-MM-dd}", response.BillingDocumentBookExpirationDate);
        logger.LogInformation("Client (from Padrón): {Name} ({Condition})", response.Client?.ClientName, response.Client?.Condition);
        logger.LogInformation("QR URL: {Qr}", response.QRCode());
    }
    else
    {
        logger.LogError("❌ ARCA rejected the request:");
        foreach (var err in response.Errors)
            logger.LogError("  - {Error}", err);
    }
}
catch (ARCAValidationException ex)
{
    logger.LogError("❌ Local validation failed:");
    foreach (var err in ex.Errors)
        logger.LogError("  - {Error}", err);
}
catch (ARCAAuthException ex)
{
    logger.LogError(ex, "❌ Authentication failed (certificate, WSAA, or signing)");
}
catch (ARCAServiceException ex)
{
    logger.LogError(ex, "❌ ARCA service returned an error (code: {Code})", ex.ErrorCode);
}
