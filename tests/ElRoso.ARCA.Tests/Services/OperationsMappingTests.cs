// EN: Tests for pure mapping helpers inside the SOAP operation wrappers.
// ES: Tests para las funciones puras de mapping dentro de los wrappers SOAP.
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;
using ElectronicMailboxOps = ElRoso.ARCA.Read.ElectronicMailboxOperations;
using InvoiceVerificationOps = ElRoso.ARCA.Read.InvoiceVerificationOperations;
using WsfeOps = ElRoso.ARCA.Billing.WsfeOperations;

namespace ElRoso.ARCA.Tests.Services;

public class OperationsMappingTests
{
    // =============================================================
    // ElectronicMailboxOperations mapping helpers
    // =============================================================

    [Theory]
    [InlineData(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO, 1)]
    [InlineData(VATConditionARCAEnum.IVA_SUJETO_EXENTO, 4)]
    [InlineData(VATConditionARCAEnum.CONSUMIDOR_FINAL, 5)]
    [InlineData(VATConditionARCAEnum.MONOTRIBUTO, 6)]
    [InlineData(VATConditionARCAEnum.SUJETO_NO_CATEGORIZADO, 7)]
    [InlineData(VATConditionARCAEnum.PROVEEDOR_DEL_EXTERIOR, 8)]
    [InlineData(VATConditionARCAEnum.CLIENTE_DEL_EXTERIOR, 9)]
    [InlineData(VATConditionARCAEnum.IVA_LIBERADO_LEY_19640, 10)]
    [InlineData(VATConditionARCAEnum.MONOTRIBUTISTA_SOCIAL, 13)]
    [InlineData(VATConditionARCAEnum.IVA_NO_ALCANZADO, 15)]
    [InlineData(VATConditionARCAEnum.MONOTRIBUTO_TRABAJADOR_INDEPENDIENTE_PROMOVIDO, 16)]
    public void ReceiverConditionCode_should_send_the_ARCA_code(VATConditionARCAEnum condition, int expected)
    {
        var client = new ClientRequest();
        client.SetCondition(condition);

        Assert.Equal(expected, WsfeOps.ReceiverConditionCode(client));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(99)]
    public void ReceiverConditionCode_should_reject_an_undefined_condition(int value)
    {
        var client = new ClientRequest();
        client.SetCondition((VATConditionARCAEnum)value);

        Action act = () => WsfeOps.ReceiverConditionCode(client);

        Assert.Throws<ARCAValidationException>(act);
    }

    [Fact]
    public void BuildFilter_should_copy_pagination_and_dates()
    {
        var req = new MailboxQueryRequest
        {
            Page = 3,
            PageSize = 25,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            Reference1 = "ref-1",
            Reference2 = "ref-2",
        };

        var filter = ElectronicMailboxOps.BuildFilter(req);

        Assert.Equal(3, filter.pagina);
        Assert.Equal(25, filter.resultadosPorPagina);
        Assert.True(filter.resultadosPorPaginaSpecified);
        Assert.Equal("2026-01-01", filter.fechaDesde);
        Assert.Equal("2026-12-31", filter.fechaHasta);
        Assert.Equal("ref-1", filter.referencia1);
        Assert.Equal("ref-2", filter.referencia2);
    }

    [Fact]
    public void BuildFilter_should_set_specified_flags_only_when_values_are_present()
    {
        var req = new MailboxQueryRequest
        {
            Page = 1,
            StateId = 5,
            HasAttachment = true,
            PublishingSystemId = 42L,
        };

        var filter = ElectronicMailboxOps.BuildFilter(req);

        Assert.True(filter.estadoSpecified);
        Assert.Equal(5, filter.estado);
        Assert.True(filter.tieneAdjuntoSpecified);
        Assert.True(filter.tieneAdjunto);
        Assert.True(filter.sistemaPublicadorIdSpecified);
        Assert.Equal(42L, filter.sistemaPublicadorId);
    }

    [Fact]
    public void BuildFilter_should_NOT_set_specified_flags_when_optionals_are_null()
    {
        var req = new MailboxQueryRequest { Page = 1 };

        var filter = ElectronicMailboxOps.BuildFilter(req);

        Assert.False(filter.estadoSpecified);
        Assert.False(filter.tieneAdjuntoSpecified);
        Assert.False(filter.sistemaPublicadorIdSpecified);
        Assert.False(filter.resultadosPorPaginaSpecified);
    }

    [Fact]
    public void MapSummary_should_copy_all_fields_and_parse_dates()
    {
        var src = new WSCComu.ComunicacionSimplificada
        {
            idComunicacion = 100,
            cuitDestinatario = 20123456789,
            fechaPublicacion = "2026-05-13",
            fechaVencimiento = "2026-05-20",
            sistemaPublicador = 42,
            sistemaPublicadorDesc = "ARCA",
            estado = 1,
            estadoDesc = "Nueva",
            asunto = "Vencimiento Monotributo",
            prioridad = 2,
            tieneAdjunto = true,
            referencia1 = "ref-a",
            referencia2 = "ref-b",
        };

        var summary = ElectronicMailboxOps.MapSummary(src);

        Assert.Equal(100, summary.Id);
        Assert.Equal(20123456789, summary.RecipientCuit);
        Assert.Equal(new DateTime(2026, 5, 13), summary.PublishedDate);
        Assert.Equal(new DateTime(2026, 5, 20), summary.ExpirationDate);
        Assert.Equal(42, summary.PublishingSystemId);
        Assert.Equal("ARCA", summary.PublishingSystem);
        Assert.Equal(1, summary.StateId);
        Assert.Equal("Nueva", summary.StateName);
        Assert.Equal("Vencimiento Monotributo", summary.Subject);
        Assert.Equal(2, summary.Priority);
        Assert.True(summary.HasAttachment);
        Assert.Equal("ref-a", summary.Reference1);
        Assert.Equal("ref-b", summary.Reference2);
    }

    [Fact]
    public void MapSummary_should_default_empty_strings_for_null_optionals()
    {
        var src = new WSCComu.ComunicacionSimplificada
        {
            idComunicacion = 1,
            sistemaPublicadorDesc = null,
            estadoDesc = null,
            asunto = null,
        };

        var summary = ElectronicMailboxOps.MapSummary(src);

        Assert.Empty(summary.PublishingSystem);
        Assert.Empty(summary.StateName);
        Assert.Empty(summary.Subject);
    }

    [Fact]
    public void MapMessage_should_include_body_and_attachments()
    {
        var src = new WSCComu.Comunicacion
        {
            idComunicacion = 200,
            asunto = "Importante",
            mensaje = "<p>Cuerpo del mensaje</p>",
            sistemaPublicadorDesc = "ARCA",
            estado = 2,
            estadoDesc = "Leída",
            tieneAdjunto = true,
            fechaPublicacion = "2026-05-14",
            adjuntos =
            [
                new WSCComu.adjunto { filename = "doc.pdf", content = [1, 2, 3] },
                new WSCComu.adjunto { filename = "ticket.xml", content = [4, 5] },
            ],
        };

        var message = ElectronicMailboxOps.MapMessage(src);

        Assert.Equal(200, message.Id);
        Assert.Equal("Importante", message.Subject);
        Assert.Equal("<p>Cuerpo del mensaje</p>", message.Body);
        Assert.Equal("ARCA", message.PublishingSystem);
        Assert.Equal("Leída", message.StateName);
        Assert.True(message.HasAttachment);
        Assert.Equal(2, message.Attachments.Count);
        Assert.Equal("doc.pdf", message.Attachments[0].FileName);
        Assert.Equal(new byte[] { 1, 2, 3 }, message.Attachments[0].Content);
        Assert.Equal("ticket.xml", message.Attachments[1].FileName);
    }

    [Fact]
    public void MapAttachment_should_handle_null_content_gracefully()
    {
        var src = new WSCComu.adjunto { filename = "empty.txt", content = null };

        var att = ElectronicMailboxOps.MapAttachment(src);

        Assert.Equal("empty.txt", att.FileName);
        Assert.Empty(att.Content);
        Assert.Empty(att.MimeType);
    }

    [Theory]
    [InlineData("2026-05-14T10:30:00", 2026, 5, 14, 10, 30, 0)]
    [InlineData("2026-05-14 10:30:00", 2026, 5, 14, 10, 30, 0)]
    [InlineData("2026-05-14", 2026, 5, 14, 0, 0, 0)]
    [InlineData("14/05/2026", 2026, 5, 14, 0, 0, 0)]
    public void TryParseDate_should_parse_common_ARCA_formats(string input, int y, int m, int d, int h, int mi, int s)
    {
        var parsed = ElectronicMailboxOps.TryParseDate(input);

        Assert.Equal(new DateTime(y, m, d, h, mi, s), parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    public void TryParseDate_should_return_null_for_unparseable(string? input)
    {
        Assert.Null(ElectronicMailboxOps.TryParseDate(input));
    }

    // =============================================================
    // InvoiceVerificationOperations mapping helpers
    // =============================================================

    [Fact]
    public void BuildCmpDatos_should_map_voucher_fields_to_WSCDC_format()
    {
        var req = new InvoiceVerificationRequest
        {
            BillingDocumentType = BillingDocumentTypeARCAEnum.FA,
            BillingDocumentBookPrefix = 3,
            BillingDocumentNumber = 42,
            BillingDocumentDate = new DateTime(2026, 5, 13),
            TotalAmount = 1210.567,                              // should round
            AuthorizationCode = "75123",
            AuthorizationMode = AuthorizationModeARCAEnum.CAE,
            IssuingCompany = new IssuingCompanyRequest
            {
                DocumentType = DocumentTypeARCAEnum.CUIT,
                DocumentNumber = 20123456789,
            },
            Receiver = new ClientRequest
            {
                DocumentType = DocumentTypeARCAEnum.DNI,
                DocumentNumber = 12345678,
            },
        };

        var datos = InvoiceVerificationOps.BuildCmpDatos(req);

        Assert.Equal("CAE", datos.CbteModo);
        Assert.Equal(20123456789, datos.CuitEmisor);
        Assert.Equal(3, datos.PtoVta);
        Assert.Equal((int)BillingDocumentTypeARCAEnum.FA, datos.CbteTipo);
        Assert.Equal(42, datos.CbteNro);
        Assert.Equal("20260513", datos.CbteFch);
        Assert.Equal(1210.57, datos.ImpTotal);                     // rounded
        Assert.Equal("75123", datos.CodAutorizacion);
        Assert.Equal("96", datos.DocTipoReceptor);                 // DNI = 96
        Assert.Equal("12345678", datos.DocNroReceptor);
    }

    [Fact]
    public void BuildCmpDatos_without_receiver_should_leave_receiver_fields_null()
    {
        var req = new InvoiceVerificationRequest
        {
            BillingDocumentType = BillingDocumentTypeARCAEnum.FB,
            BillingDocumentBookPrefix = 1,
            BillingDocumentNumber = 1,
            BillingDocumentDate = new DateTime(2026, 1, 1),
            TotalAmount = 100,
            AuthorizationCode = "75999",
            AuthorizationMode = AuthorizationModeARCAEnum.CAEA,
            IssuingCompany = new IssuingCompanyRequest { DocumentNumber = 20111 },
            Receiver = null,
        };

        var datos = InvoiceVerificationOps.BuildCmpDatos(req);

        Assert.Equal("CAEA", datos.CbteModo);
        Assert.Null(datos.DocTipoReceptor);
        Assert.Null(datos.DocNroReceptor);
    }

    [Fact]
    public void BuildCmpDatos_with_RequesterCuit_should_keep_issuer_as_CuitEmisor()
    {
        var req = new InvoiceVerificationRequest
        {
            BillingDocumentType = BillingDocumentTypeARCAEnum.FA,
            BillingDocumentBookPrefix = 1,
            BillingDocumentNumber = 1,
            BillingDocumentDate = new DateTime(2026, 1, 1),
            TotalAmount = 100,
            AuthorizationCode = "75999",
            IssuingCompany = new IssuingCompanyRequest { DocumentNumber = 20123456789 },
            RequesterCuit = 33000000000,
        };

        var datos = InvoiceVerificationOps.BuildCmpDatos(req);

        Assert.Equal(20123456789, datos.CuitEmisor);
    }

    [Theory]
    [InlineData("20260513", 2026, 5, 13)]
    [InlineData("20260513120000", 2026, 5, 13)]
    public void TryParseFchProceso_should_parse_ARCA_date_formats(string input, int y, int m, int d)
    {
        var parsed = InvoiceVerificationOps.TryParseFchProceso(input);

        Assert.NotNull(parsed);
        Assert.Equal(y, parsed!.Value.Year);
        Assert.Equal(m, parsed.Value.Month);
        Assert.Equal(d, parsed.Value.Day);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    public void TryParseFchProceso_should_return_null_for_invalid_input(string? input)
    {
        Assert.Null(InvoiceVerificationOps.TryParseFchProceso(input));
    }

    // =============================================================
    // WsfeOperations mapping helpers
    // EN: FECompConsultar returns FchVto as yyyyMMdd but FchProceso as yyyyMMddHHmmss — the
    //     parser has to take both or a reconciled CAE loses its expiration date.
    // ES: FECompConsultar devuelve FchVto como yyyyMMdd pero FchProceso como yyyyMMddHHmmss —
    //     el parser tiene que aceptar ambos o un CAE reconciliado pierde su vencimiento.
    // =============================================================

    [Theory]
    [InlineData("20260802", 2026, 8, 2, 0, 0, 0)]
    [InlineData("20260723103949", 2026, 7, 23, 10, 39, 49)]
    public void TryParseArcaDate_should_parse_both_ARCA_date_formats(
        string input, int y, int mo, int d, int h, int mi, int sec)
    {
        var parsed = WsfeOps.TryParseArcaDate(input);

        Assert.NotNull(parsed);
        Assert.Equal(new DateTime(y, mo, d, h, mi, sec), parsed!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("2026-08-02")]
    public void TryParseArcaDate_should_return_null_for_invalid_input(string? input)
    {
        Assert.Null(WsfeOps.TryParseArcaDate(input));
    }
}
