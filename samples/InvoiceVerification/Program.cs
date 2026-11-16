// EN: Verifies an invoice received from a vendor against ARCA WSCDC.
// ES: Verifica una factura recibida de un proveedor contra el WSCDC de ARCA.
using ElRoso.ARCA;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "ARCA_")
    .Build();

var arca = config.GetSection("Arca");
var sample = config.GetSection("Sample");

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
var verifier = sp.GetRequiredService<IInvoiceVerificationService>();

// EN: Voucher data you received from a vendor (typically read from the PDF / QR / electronic file).
// ES: Datos del comprobante que recibiste del proveedor (típicamente leídos del PDF / QR / archivo electrónico).
var request = new InvoiceVerificationRequest
{
    BillingDocumentType = Enum.Parse<BillingDocumentTypeARCAEnum>(sample["BillingDocumentType"] ?? "FA"),
    BillingDocumentBookPrefix = sample.GetValue<int>("BillingDocumentBookPrefix"),
    BillingDocumentNumber = sample.GetValue<long>("BillingDocumentNumber"),
    BillingDocumentDate = sample.GetValue<DateTime>("BillingDocumentDate"),
    TotalAmount = sample.GetValue<double>("TotalAmount"),
    AuthorizationCode = sample["AuthorizationCode"]!,
    AuthorizationMode = AuthorizationModeARCAEnum.CAE,
    IssuingCompany = new IssuingCompanyRequest
    {
        DocumentType = DocumentTypeARCAEnum.CUIT,
        DocumentNumber = sample.GetValue<long>("IssuerCuit"),
    },
};

logger.LogInformation("Verifying voucher {Type} {Book}-{Number} from issuer {Cuit}...",
    request.BillingDocumentType, request.BillingDocumentBookPrefix, request.BillingDocumentNumber, request.IssuingCompany.DocumentNumber);

try
{
    var response = await verifier.VerifyAsync(request);

    if (response.IsAuthorized)
    {
        logger.LogInformation("✅ Voucher is AUTHORIZED by ARCA (Resultado: {Result}, Processed: {Date:yyyy-MM-dd})",
            response.Resultado, response.ProcessedDate);
    }
    else
    {
        logger.LogWarning("❌ Voucher NOT authorized (Resultado: {Result})", response.Resultado);
        foreach (var obs in response.Observations)
            logger.LogWarning("  Observation: {Obs}", obs);
        foreach (var err in response.Errors)
            logger.LogError("  Error: {Err}", err);
    }
}
catch (ARCAValidationException ex)
{
    logger.LogError("❌ Local validation failed:");
    foreach (var err in ex.Errors) logger.LogError("  - {Error}", err);
}
catch (ARCAAuthException ex)
{
    logger.LogError(ex, "❌ Authentication failed");
}
catch (ARCAServiceException ex)
{
    logger.LogError(ex, "❌ ARCA service error (code: {Code})", ex.ErrorCode);
}
