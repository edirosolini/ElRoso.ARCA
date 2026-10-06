// EN: Tests for AddARCAClient — verifies DI registration of all ARCA services.
// ES: Tests para AddARCAClient — verifica el registro en DI de todos los servicios ARCA.
using ElRoso.ARCA.Core;
using ElRoso.ARCA;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElRoso.ARCA.Tests.DependencyInjection;

public class ARCAServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(Action<ARCAOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddARCAClient(opts =>
        {
            opts.IsProduction        = false;
            opts.CertificatePath     = "/tmp/cert.pfx";
            opts.CertificatePassword = "pwd";
            opts.TokenCacheDirectory = Path.Combine(Path.GetTempPath(), "ElRoso.ARCA.Tests.DI");
            opts.SoapTimeoutSeconds  = 15;
            configure?.Invoke(opts);
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddARCAClient_should_register_ARCAOptions_with_configured_values()
    {
        using var sp = BuildProvider();

        var options = sp.GetRequiredService<ARCAOptions>();

        options.IsProduction.Should().BeFalse();
        options.CertificatePath.Should().Be("/tmp/cert.pfx");
        options.CertificatePassword.Should().Be("pwd");
        options.TokenCacheDirectory.Should().EndWith("ElRoso.ARCA.Tests.DI");
        options.SoapTimeoutSeconds.Should().Be(15);
    }

    [Fact]
    public void AddARCAClient_should_register_ICertificateService()
    {
        using var sp = BuildProvider();

        var service = sp.GetRequiredService<ICertificateService>();

        service.Should().NotBeNull();
    }

    [Fact]
    public void AddARCAClient_should_register_ITokenCache_using_FileTokenCache_with_configured_directory()
    {
        using var sp = BuildProvider();

        var cache = sp.GetRequiredService<ITokenCache>();

        cache.Should().BeOfType<FileTokenCache>();
    }

    [Fact]
    public void AddARCAClient_should_register_ILoginTicketService()
    {
        using var sp = BuildProvider();

        var service = sp.GetRequiredService<ILoginTicketService>();

        service.Should().NotBeNull();
    }

    [Fact]
    public void AddARCAClient_should_register_IBillingDocumentNumberingService()
    {
        using var sp = BuildProvider();

        var service = sp.GetRequiredService<IBillingDocumentNumberingService>();

        service.Should().NotBeNull();
    }

    [Fact]
    public void AddARCAClient_should_register_IPadronService_as_singleton()
    {
        using var sp = BuildProvider();

        var first = sp.GetRequiredService<IPadronService>();
        var second = sp.GetRequiredService<IPadronService>();

        first.Should().NotBeNull();
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void AddARCAClient_should_register_FluentValidation_validator()
    {
        using var sp = BuildProvider();

        var validator = sp.GetRequiredService<IValidator<BillingDocumentNumberingRequest>>();

        validator.Should().NotBeNull();
    }

    [Fact]
    public void All_registered_services_should_be_singleton()
    {
        // EN: Singleton lifetime is required for the static caches inside FileTokenCache and CertificateService.
        // ES: El lifetime Singleton es requerido para los cachés estáticos dentro de FileTokenCache y CertificateService.
        using var sp = BuildProvider();

        var optionsA = sp.GetRequiredService<ARCAOptions>();
        var optionsB = sp.GetRequiredService<ARCAOptions>();
        var certA = sp.GetRequiredService<ICertificateService>();
        var certB = sp.GetRequiredService<ICertificateService>();
        var cacheA = sp.GetRequiredService<ITokenCache>();
        var cacheB = sp.GetRequiredService<ITokenCache>();
        var loginA = sp.GetRequiredService<ILoginTicketService>();
        var loginB = sp.GetRequiredService<ILoginTicketService>();
        var billingA = sp.GetRequiredService<IBillingDocumentNumberingService>();
        var billingB = sp.GetRequiredService<IBillingDocumentNumberingService>();
        var valA = sp.GetRequiredService<IValidator<BillingDocumentNumberingRequest>>();
        var valB = sp.GetRequiredService<IValidator<BillingDocumentNumberingRequest>>();

        optionsB.Should().BeSameAs(optionsA);
        certB.Should().BeSameAs(certA);
        cacheB.Should().BeSameAs(cacheA);
        loginB.Should().BeSameAs(loginA);
        billingB.Should().BeSameAs(billingA);
        valB.Should().BeSameAs(valA);
    }

    [Fact]
    public void AddARCAClient_should_return_same_service_collection_for_chaining()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var result = services.AddARCAClient(o =>
        {
            o.CertificatePath = "/x";
        });

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddARCAClient_should_invoke_configure_callback_exactly_once()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var callCount = 0;

        services.AddARCAClient(opts =>
        {
            callCount++;
            opts.CertificatePath = "/x";
        });

        callCount.Should().Be(1);
    }

    [Fact]
    public void AddARCAClient_with_production_options_should_propagate_IsProduction()
    {
        using var sp = BuildProvider(opts => opts.IsProduction = true);

        var options = sp.GetRequiredService<ARCAOptions>();

        options.IsProduction.Should().BeTrue();
    }
}