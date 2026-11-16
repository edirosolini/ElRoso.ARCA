// EN: Tests for ARCAServiceException - thrown when ARCA web services return errors.
// ES: Tests para ARCAServiceException - error devuelto por los WS de ARCA.
using ElRoso.ARCA.Core;

namespace ElRoso.ARCA.Tests.Exceptions;

public class ARCAServiceExceptionTests
{
    [Fact]
    public void Constructor_with_message_only_should_have_null_error_code()
    {
        var exception = new ARCAServiceException("Service unavailable");

        Assert.Equal("Service unavailable", exception.Message);
        Assert.Null(exception.ErrorCode);
    }

    [Fact]
    public void Constructor_with_inner_exception_should_preserve_chain()
    {
        var inner = new TimeoutException();

        var exception = new ARCAServiceException("SOAP timeout", inner);

        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void Constructor_with_error_code_and_message_should_format_message_and_keep_code()
    {
        var exception = new ARCAServiceException(1503, "Invalid CUIT");

        Assert.Equal(1503, exception.ErrorCode);
        Assert.Contains("1503", exception.Message);
        Assert.Contains("Invalid CUIT", exception.Message);
    }
}
