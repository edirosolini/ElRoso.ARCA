// <copyright file="ConfigureServices.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA;

using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

public static class ARCAServiceCollectionExtensions
{
    /// <summary>
    /// Registers all ARCA (ex-AFIP) services into the DI container.
    /// Registra todos los servicios de ARCA (ex-AFIP) en el contenedor de DI.
    /// </summary>
    /// <example>
    /// builder.Services.AddARCAClient(options =>
    /// {
    ///     options.IsProduction        = false;
    ///     options.CertificatePath     = "/certs/empresa.pfx";
    ///     options.CertificatePassword = "miPassword";
    ///     options.TokenCacheDirectory = "/tmp/ARCA-tokens";
    /// });
    /// </example>
    public static IServiceCollection AddARCAClient(
        this IServiceCollection services,
        Action<ARCAOptions> configure)
    {
        var options = new ARCAOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<ICertificateService, CertificateService>();

        // EN: The login ticket cache is encrypted at rest with IDataProtection. Every registration
        //     inside AddDataProtection() is a TryAdd, so a host that already configured its own
        //     key ring (persisted volume, key escrow) keeps winning — this only guarantees that a
        //     provider exists at all.
        // ES: La caché de login tickets se cifra en reposo con IDataProtection. Todo lo que
        //     registra AddDataProtection() es TryAdd, así que un host que ya configuró su propio
        //     key ring (volumen persistido, escrow) sigue mandando — esto solo garantiza que haya
        //     un provider.
        services.AddDataProtection();
        services.AddSingleton<ITokenCache, FileTokenCache>(sp =>
            new FileTokenCache(
                options.TokenCacheDirectory,
                sp.GetRequiredService<IDataProtectionProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileTokenCache>>()));

        services.AddSingleton<ILoginTicketService, LoginTicketService>();

        // SOAP operation wrappers — separated so consumer services are unit-testable.
        // Wrappers de operaciones SOAP — separados para que los services consumidores sean testeables.
        services.AddSingleton<IPadronOperations, PadronOperations>();
        services.AddSingleton<IWsfeOperations, WsfeOperations>();
        services.AddSingleton<IWsfexOperations, WsfexOperations>();
        services.AddSingleton<IInvoiceVerificationOperations, InvoiceVerificationOperations>();
        services.AddSingleton<IElectronicMailboxOperations, ElectronicMailboxOperations>();

        services.AddSingleton<IBillingDocumentNumberingService, BillingDocumentNumberingService>();
        services.AddSingleton<IPadronService, PadronService>();
        services.AddSingleton<IInvoiceVerificationService, InvoiceVerificationService>();
        services.AddSingleton<IElectronicMailboxService, ElectronicMailboxService>();

        // Singleton: validators are stateless and safe to reuse.
        // Singleton: los validadores son stateless y se pueden reusar.
        services.AddSingleton<IValidator<BillingDocumentNumberingRequest>, BillingDocumentNumberingValidator>();
        services.AddSingleton<IValidator<InvoiceVerificationRequest>, InvoiceVerificationValidator>();

        return services;
    }
}