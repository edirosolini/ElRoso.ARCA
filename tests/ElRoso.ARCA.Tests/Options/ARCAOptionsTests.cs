// EN: Tests for ARCAOptions - defaults and environment URL resolution.
// ES: Tests para ARCAOptions - defaults y resolución de URLs por ambiente.
using ElRoso.ARCA.Core;

namespace ElRoso.ARCA.Tests.Options;

public class ARCAOptionsTests
{
    [Fact]
    public void Defaults_should_target_homologation_and_safe_values()
    {
        var options = new ARCAOptions();

        Assert.False(options.IsProduction);
        Assert.Equal(30, options.SoapTimeoutSeconds);
        Assert.Empty(options.CertificatePath);
        Assert.Null(options.CertificatePassword);
        Assert.Equal(Path.GetTempPath(), options.TokenCacheDirectory);
    }

    [Theory]
    [InlineData(false, "wsaahomo.afip.gov.ar")]
    [InlineData(true, "wsaa.afip.gov.ar")]
    public void WsaaUrl_should_switch_by_environment(bool isProduction, string expectedHost)
    {
        var options = new ARCAOptions { IsProduction = isProduction };

        var url = typeof(ARCAOptions)
            .GetProperty("WsaaUrl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(options) as string;

        Assert.Contains(expectedHost, url);
    }

    [Theory]
    [InlineData(false, "wswhomo.afip.gov.ar")]
    [InlineData(true, "servicios1.afip.gov.ar")]
    public void WsfeUrl_should_switch_by_environment(bool isProduction, string expectedHost)
    {
        var options = new ARCAOptions { IsProduction = isProduction };

        var url = typeof(ARCAOptions)
            .GetProperty("WsfeUrl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(options) as string;

        Assert.Contains(expectedHost, url);
    }

    [Theory]
    [InlineData(false, "wswhomo.afip.gov.ar")]
    [InlineData(true, "servicios1.afip.gov.ar")]
    public void WsfexUrl_should_switch_by_environment(bool isProduction, string expectedHost)
    {
        var options = new ARCAOptions { IsProduction = isProduction };

        var url = typeof(ARCAOptions)
            .GetProperty("WsfexUrl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(options) as string;

        Assert.Contains(expectedHost, url);
    }

    [Theory]
    [InlineData(false, "awshomo.afip.gov.ar")]
    [InlineData(true, "aws.afip.gov.ar")]
    public void PadronUrl_should_switch_by_environment(bool isProduction, string expectedHost)
    {
        var options = new ARCAOptions { IsProduction = isProduction };

        var url = typeof(ARCAOptions)
            .GetProperty("PadronUrl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(options) as string;

        Assert.Contains(expectedHost, url);
    }

    [Theory]
    [InlineData(false, "stable-middleware-tecno-ext.afip.gob.ar")]
    [InlineData(true, "infraestructura.afip.gob.ar")]
    public void WsccomuUrl_should_switch_by_environment(bool isProduction, string expectedHost)
    {
        var options = new ARCAOptions { IsProduction = isProduction };

        Assert.Contains(expectedHost, options.WsccomuUrl);
    }

    [Fact]
    public void WsccomuUrl_should_respect_override_when_set()
    {
        var options = new ARCAOptions
        {
            IsProduction = true,
            WsccomuUrl = "https://my-proxy.example.com/ve-ws/services/veconsumer",
        };

        Assert.Equal("https://my-proxy.example.com/ve-ws/services/veconsumer", options.WsccomuUrl);
    }

    [Fact]
    public void WsccomuUrl_should_fall_back_to_default_when_override_cleared()
    {
        var options = new ARCAOptions
        {
            IsProduction = true,
            WsccomuUrl = "https://my-proxy.example.com/...",
        };

        // Clearing the override should restore the environment-based default.
        options.WsccomuUrl = string.Empty;

        Assert.Contains("infraestructura.afip.gob.ar", options.WsccomuUrl);
    }
}
