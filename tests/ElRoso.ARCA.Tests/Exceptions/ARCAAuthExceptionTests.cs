// EN: Tests for ARCAAuthException - thrown on WSAA / certificate / signing failures.
// ES: Tests para ARCAAuthException - falla de WSAA / certificado / firma.
using ElRoso.ARCA.Core;

namespace ElRoso.ARCA.Tests.Exceptions;

public class ARCAAuthExceptionTests
{
    [Fact]
    public void Constructor_with_message_should_set_message()
    {
        var exception = new ARCAAuthException("Certificate not found");

        Assert.Equal("Certificate not found", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Constructor_with_inner_exception_should_preserve_chain()
    {
        var inner = new InvalidOperationException("PKCS7 signing failed");

        var exception = new ARCAAuthException("WSAA login failed", inner);

        Assert.Equal("WSAA login failed", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
