// EN: Tests for BillingDocumentNumberingResponse — FromRequest factory and QRCode generation.
// ES: Tests para BillingDocumentNumberingResponse — factory FromRequest y generación del QR.
using System.Text;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;
using Newtonsoft.Json.Linq;

namespace ElRoso.ARCA.Tests.Domains.Responses;

public class BillingDocumentNumberingResponseTests
{
    private static BillingDocumentNumberingRequest SampleRequest() => new()
    {
        BillingDocumentType = BillingDocumentTypeARCAEnum.FA,
        BillingDocumentBookPrefix = 3,
        BillingDocumentDate = new DateTime(2026, 5, 13),
        Currency = "Pesos",
        ExchangeRate = 1,
        ConceptType = ConceptTypeARCAEnum.Products,
        AmountTax = 1210.00,
        Version = 1,
        IssuingCompany = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
        Client = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 30987654321 },
    };

    [Fact]
    public void Default_Errors_should_be_empty_list()
    {
        var response = new BillingDocumentNumberingResponse();

        Assert.Empty(response.Errors);
        Assert.False(response.Result);
        Assert.Equal(1, response.Version);
    }

    [Fact]
    public void FromRequest_should_copy_echo_fields()
    {
        var request = SampleRequest();

        var response = BillingDocumentNumberingResponse.FromRequest(request);

        Assert.Equal(request.BillingDocumentType, response.BillingDocumentType);
        Assert.Equal(request.BillingDocumentDate, response.BillingDocumentDate);
        Assert.Equal(request.BillingDocumentBookPrefix, response.BillingDocumentBookPrefix);
        Assert.Equal(request.Currency, response.Currency);
        Assert.Equal(request.ExchangeRate, response.ExchangeRate);
        Assert.Equal(request.Total, response.Total);
        Assert.Equal(request.Version, response.Version);
        Assert.Same(request.IssuingCompany, response.IssuingCompany);
        Assert.Same(request.Client, response.Client);
    }

    [Fact]
    public void QRCode_should_return_null_when_CAE_is_null()
    {
        var response = BillingDocumentNumberingResponse.FromRequest(SampleRequest());
        response.CAE = null;

        Assert.Null(response.QRCode());
    }

    [Fact]
    public void QRCode_should_return_null_when_CAE_is_empty()
    {
        var response = BillingDocumentNumberingResponse.FromRequest(SampleRequest());
        response.CAE = string.Empty;

        Assert.Null(response.QRCode());
    }

    [Fact]
    public void QRCode_should_return_null_when_IssuingCompany_is_null()
    {
        var response = BillingDocumentNumberingResponse.FromRequest(SampleRequest());
        response.CAE = "75123456789012";
        response.IssuingCompany = null;

        Assert.Null(response.QRCode());
    }

    [Fact]
    public void QRCode_should_return_null_when_Client_is_null()
    {
        var response = BillingDocumentNumberingResponse.FromRequest(SampleRequest());
        response.CAE = "75123456789012";
        response.Client = null;

        Assert.Null(response.QRCode());
    }

    [Fact]
    public void QRCode_should_return_url_starting_with_AFIP_base()
    {
        var response = BillingDocumentNumberingResponse.FromRequest(SampleRequest());
        response.CAE = "75123456789012";
        response.BillingDocumentNumber = 42;

        var url = response.QRCode();

        Assert.StartsWith("https://www.afip.gob.ar/fe/qr/?p=", url);
    }

    [Fact]
    public void QRCode_payload_should_decode_to_expected_AFIP_schema()
    {
        // EN: Decode the base64 payload and verify every field in the AFIP schema.
        // ES: Decodifica el base64 y verifica cada campo del schema AFIP.
        var request = SampleRequest();
        var response = BillingDocumentNumberingResponse.FromRequest(request);
        response.CAE = "75123456789012";
        response.BillingDocumentNumber = 42;

        var url = response.QRCode()!;
        var base64 = url["https://www.afip.gob.ar/fe/qr/?p=".Length..];
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        var payload = JObject.Parse(json);

        Assert.Equal(1, payload["ver"]!.Value<int>());
        Assert.Equal("2026-05-13", payload["fecha"]!.Value<string>());
        Assert.Equal(20123456789, payload["cuit"]!.Value<long>());
        Assert.Equal(3, payload["ptoVta"]!.Value<int>());
        Assert.Equal((int)BillingDocumentTypeARCAEnum.FA, payload["tipoCmp"]!.Value<int>());
        Assert.Equal(42, payload["nroCmp"]!.Value<int>());
        Assert.Equal(request.Total, payload["importe"]!.Value<double>());
        Assert.Equal("Pesos", payload["moneda"]!.Value<string>());
        Assert.Equal(1, payload["ctz"]!.Value<double>());
        Assert.Equal((int)DocumentTypeARCAEnum.CUIT, payload["tipoDocRec"]!.Value<int>());
        Assert.Equal(30987654321, payload["nroDocRec"]!.Value<long>());
        Assert.Equal("E", payload["tipoCodAut"]!.Value<string>());
        Assert.Equal(75123456789012, payload["codAut"]!.Value<long>());
    }

    [Fact]
    public void QRCode_should_format_date_as_ISO_yyyy_MM_dd()
    {
        var request = SampleRequest();
        request.BillingDocumentDate = new DateTime(2026, 3, 5);
        var response = BillingDocumentNumberingResponse.FromRequest(request);
        response.CAE = "75123456789012";

        var url = response.QRCode()!;
        var base64 = url["https://www.afip.gob.ar/fe/qr/?p=".Length..];
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));

        Assert.Contains("\"fecha\":\"2026-03-05\"", json);
    }

    [Fact]
    public void QRCode_should_use_export_currency_and_exchange_rate()
    {
        // EN: Export invoices use foreign currency and an exchange rate.
        // ES: Las facturas de exportación usan moneda extranjera y cotización.
        var request = SampleRequest();
        request.BillingDocumentType = BillingDocumentTypeARCAEnum.InvoiceExport;
        request.BillingDocumentId = 1;
        request.Items = [new() { ItemDescription = "Software", Amount = 1000 }];
        request.Currency = "Dolares";
        request.ExchangeRate = 1050.50;
        request.AmountTax = 1000;

        var response = BillingDocumentNumberingResponse.FromRequest(request);
        response.CAE = "75999999999999";

        var url = response.QRCode()!;
        var base64 = url["https://www.afip.gob.ar/fe/qr/?p=".Length..];
        var payload = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));

        Assert.Equal("Dolares", payload["moneda"]!.Value<string>());
        Assert.Equal(1050.50, payload["ctz"]!.Value<double>());
        Assert.Equal((int)BillingDocumentTypeARCAEnum.InvoiceExport, payload["tipoCmp"]!.Value<int>());
    }
}
