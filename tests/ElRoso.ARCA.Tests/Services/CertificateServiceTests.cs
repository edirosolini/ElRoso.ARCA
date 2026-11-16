// EN: Tests for CertificateService — loads X.509 from disk, caches in memory, signs PKCS#7.
// ES: Tests para CertificateService — carga X.509 desde disco, cachea en memoria, firma PKCS#7.
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElRoso.ARCA.Tests.Services;

public class CertificateServiceTests : IDisposable
{
    private readonly string tempDir;
    private readonly CertificateService service;

    public CertificateServiceTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ElRoso.ARCA.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        service = new CertificateService(NullLogger<CertificateService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// EN: Generates a self-signed X.509 cert and writes it as .pfx to a temp path.
    /// ES: Genera un cert X.509 self-signed y lo escribe como .pfx en un path temporal.
    /// </summary>
    private string CreateSelfSignedPfx(string? password, string subject = "CN=TestCertificate")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));

        var pfxBytes = password is null
            ? cert.Export(X509ContentType.Pfx)
            : cert.Export(X509ContentType.Pfx, password);

        var path = Path.Combine(tempDir, $"cert-{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(path, pfxBytes);
        return path;
    }

    [Fact]
    public void LoadCertificate_with_password_should_load_pfx_from_disk()
    {
        var path = CreateSelfSignedPfx(password: "testpass");

        var cert = service.LoadCertificate(path, "testpass");

        Assert.NotNull(cert);
        Assert.Contains("TestCertificate", cert.Subject);
        Assert.True(cert.HasPrivateKey);
    }

    [Fact]
    public void LoadCertificate_without_password_should_load_unencrypted_pfx()
    {
        var path = CreateSelfSignedPfx(password: null);

        var cert = service.LoadCertificate(path, password: null);

        Assert.NotNull(cert);
        Assert.True(cert.HasPrivateKey);
    }

    [Fact]
    public void LoadCertificate_should_cache_by_path_and_return_same_instance()
    {
        // EN: The cert cache key is the file path, so the second call returns the cached instance.
        // ES: La clave del caché es la ruta, así que la segunda llamada devuelve la instancia cacheada.
        var path = CreateSelfSignedPfx(password: "testpass");

        var first = service.LoadCertificate(path, "testpass");
        var second = service.LoadCertificate(path, "testpass");

        Assert.Same(first, second);
    }

    [Fact]
    public void LoadCertificate_with_missing_file_should_throw_ARCAAuthException()
    {
        var missingPath = Path.Combine(tempDir, "does-not-exist.pfx");

        var act = () => service.LoadCertificate(missingPath, "anypass");

        var ex = Assert.Throws<ARCAAuthException>(act);
        Assert.Contains(missingPath, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsAssignableFrom<Exception>(ex.InnerException);
    }

    [Fact]
    public void LoadCertificate_with_wrong_password_should_throw_ARCAAuthException()
    {
        var path = CreateSelfSignedPfx(password: "correctpass");

        var act = () => service.LoadCertificate(path, "wrongpass");

        var ex = Assert.Throws<ARCAAuthException>(act);
        Assert.IsAssignableFrom<Exception>(ex.InnerException);
    }

    [Fact]
    public void SignMessage_should_produce_valid_PKCS7_signature()
    {
        var path = CreateSelfSignedPfx(password: "testpass");
        var cert = service.LoadCertificate(path, "testpass");
        var payload = System.Text.Encoding.UTF8.GetBytes("<loginTicketRequest></loginTicketRequest>");

        var signed = service.SignMessage(payload, cert);

        Assert.NotNull(signed);
        Assert.True(signed.Length > payload.Length);

        // EN: Verify it's a valid SignedCms structure by decoding it.
        // ES: Verifica que sea una estructura SignedCms válida decodificándola.
        var verifyCms = new SignedCms();
        var decode = () => verifyCms.Decode(signed);
        Assert.Null(Record.Exception(decode));
        Assert.Equal(payload, verifyCms.ContentInfo.Content);
    }

    [Fact]
    public void SignMessage_with_disposed_cert_should_throw_ARCAAuthException()
    {
        var path = CreateSelfSignedPfx(password: "testpass");
        var cert = service.LoadCertificate(path, "testpass");
        var clone = X509CertificateLoader.LoadPkcs12FromFile(path, "testpass");
        clone.Dispose();

        var act = () => service.SignMessage([1, 2, 3], clone);

        var ex = Assert.Throws<ARCAAuthException>(act);
        Assert.Contains("sign", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
