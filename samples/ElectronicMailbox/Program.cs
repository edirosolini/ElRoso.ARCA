// EN: Lists notifications from ARCA's e-Ventanilla (DFE). Optionally consumes the first unread.
// ES: Lista notificaciones del e-Ventanilla de ARCA (DFE). Opcionalmente consume la primera no leída.
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
var issuerCuit = long.Parse(config["Sample:IssuerCuit"] ?? throw new InvalidOperationException("Missing Sample:IssuerCuit"));
var consumeFirst = config.GetValue<bool>("Sample:ConsumeFirstUnread");

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));
services.AddARCAClient(options =>
{
    options.IsProduction = arca.GetValue<bool>("IsProduction");
    options.CertificatePath = arca["CertificatePath"]!;
    options.CertificatePassword = arca["CertificatePassword"];
    options.TokenCacheDirectory = arca["TokenCacheDirectory"] ?? Path.GetTempPath();
    options.SoapTimeoutSeconds = arca.GetValue<int>("SoapTimeoutSeconds", 30);
    // EN: WsccomuUrl is now auto-resolved from IsProduction — no manual override required.
    //     Set options.WsccomuUrl only if ARCA migrates the endpoint or you proxy through middleware.
    // ES: WsccomuUrl ahora se resuelve automáticamente desde IsProduction — sin override manual.
    //     Setealo solo si ARCA migra la URL o si proxyás a través de middleware propio.
});

await using var sp = services.BuildServiceProvider();
var logger = sp.GetRequiredService<ILogger<Program>>();
var mailbox = sp.GetRequiredService<IElectronicMailboxService>();

var issuer = new IssuingCompanyRequest
{
    DocumentType = DocumentTypeARCAEnum.CUIT,
    DocumentNumber = issuerCuit,
};

try
{
    logger.LogInformation("Listing notifications for CUIT {Cuit}...", issuerCuit);
    var page1 = await mailbox.ListAsync(new MailboxQueryRequest
    {
        IssuingCompany = issuer,
        Page = 1,
        PageSize = 20,
    });

    logger.LogInformation("📬 {Total} notifications total. Page 1/{Pages}, {Count} shown:",
        page1.TotalItems, page1.TotalPages, page1.Messages.Count);

    foreach (var msg in page1.Messages)
    {
        logger.LogInformation("  [{Id}] {Date:yyyy-MM-dd} {State,-12} {Subject}",
            msg.Id, msg.PublishedDate, $"({msg.StateName})", msg.Subject);
    }

    if (consumeFirst && page1.Messages.Count > 0)
    {
        var first = page1.Messages[0];
        logger.LogInformation("📖 Consuming first message: [{Id}] {Subject}...", first.Id, first.Subject);
        var detail = await mailbox.ConsumeAsync(new MailboxConsumeRequest
        {
            IssuingCompany = issuer,
            MessageId = first.Id,
        });

        if (detail.Success && detail.Message is not null)
        {
            logger.LogInformation("--- BODY ---");
            logger.LogInformation("{Body}", detail.Message.Body);
            logger.LogInformation("--- END BODY ---");
            if (detail.Message.HasAttachment)
                logger.LogInformation("📎 {Count} attachment(s)", detail.Message.Attachments.Count);
        }
        else
        {
            foreach (var err in detail.Errors) logger.LogError("  - {Err}", err);
        }
    }
}
catch (ARCAValidationException ex)
{
    foreach (var err in ex.Errors) logger.LogError("Validation: {Err}", err);
}
catch (ARCAAuthException ex)
{
    logger.LogError(ex, "❌ Auth failed");
}
catch (ARCAServiceException ex)
{
    logger.LogError(ex, "❌ ARCA service error (code: {Code})", ex.ErrorCode);
}
