// EN: Tests for BillingDocumentNumberingService — covers validation gate and routing branches
// that don't depend on real SOAP clients. Deep SOAP-touching coverage requires extracting a
// factory for the WCF clients (deferred to a future refactor).
// ES: Tests para BillingDocumentNumberingService — cubren el gate de validación y las ramas de
// routing que no dependen de los SOAP clients reales. La cobertura profunda requiere extraer
// un factory para los clientes WCF (diferido a refactor futuro).
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElRoso.ARCA.Tests.Services;

public class BillingDocumentNumberingServiceTests
{
    private readonly Mock<ILoginTicketService> loginTicketServiceMock = new();
    private readonly Mock<ITokenCache> tokenCacheMock = new();
    private readonly Mock<IValidator<BillingDocumentNumberingRequest>> validatorMock = new();
    private readonly Mock<IPadronOperations> padronMock = new();
    private readonly Mock<IWsfeOperations> wsfeMock = new();
    private readonly Mock<IWsfexOperations> wsfexMock = new();
    private readonly ARCAOptions options = new()
    {
        IsProduction = false,
        CertificatePath = "/dev/null",
        CertificatePassword = null,
        TokenCacheDirectory = Path.GetTempPath(),
        SoapTimeoutSeconds = 5,
    };

    private BillingDocumentNumberingService CreateService() => new(
        NullLogger<BillingDocumentNumberingService>.Instance,
        loginTicketServiceMock.Object,
        tokenCacheMock.Object,
        options,
        validatorMock.Object,
        padronMock.Object,
        wsfeMock.Object,
        wsfexMock.Object);

    private static BillingDocumentNumberingRequest MinimalRequest(BillingDocumentTypeARCAEnum type) => new()
    {
        BillingDocumentType       = type,
        BillingDocumentBookPrefix = 1,
        BillingDocumentDate       = new DateTime(2026, 5, 13),
        Currency                  = "Pesos",
        ExchangeRate              = 1,
        ConceptType               = ConceptTypeARCAEnum.Products,
        AmountTax                 = 100,
        IssuingCompany = new() { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
        Client         = new() { DocumentType = DocumentTypeARCAEnum.SIN_IDENTIFICAR, DocumentNumber = 0 },
    };

    [Fact]
    public void Constructor_should_accept_all_dependencies_without_throwing()
    {
        var act = CreateService;

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AuthorizeAsync_should_throw_ARCAValidationException_when_validator_fails()
    {
        validatorMock
            .Setup(v => v.Validate(It.IsAny<BillingDocumentNumberingRequest>()))
            .Returns(new ValidationResult(new[]
            {
                new ValidationFailure("Currency", "Currency is not supported") { ErrorCode = "CUR001" },
                new ValidationFailure("AmountTax", "AmountTax must be positive") { ErrorCode = "AMT002" },
            }));

        var service = CreateService();

        var act = () => service.AuthorizeAsync(MinimalRequest(BillingDocumentTypeARCAEnum.FA));

        var exception = await act.Should().ThrowAsync<ARCAValidationException>();
        exception.Which.Errors.Should().HaveCount(2);
        exception.Which.Errors.Should().Contain(e => e.Contains("CUR001") && e.Contains("Currency is not supported"));
        exception.Which.Errors.Should().Contain(e => e.Contains("AMT002") && e.Contains("AmountTax must be positive"));
    }

    [Fact]
    public async Task AuthorizeAsync_should_skip_SOAP_when_validation_fails()
    {
        // EN: When validation fails the service must not consult the token cache or hit WSAA.
        // ES: Cuando la validación falla, el service no debe consultar el caché de tokens ni WSAA.
        validatorMock
            .Setup(v => v.Validate(It.IsAny<BillingDocumentNumberingRequest>()))
            .Returns(new ValidationResult([new ValidationFailure("Field", "bad")]));

        var service = CreateService();

        await Assert.ThrowsAsync<ARCAValidationException>(
            () => service.AuthorizeAsync(MinimalRequest(BillingDocumentTypeARCAEnum.FA)));

        tokenCacheMock.Verify(t => t.GetAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        loginTicketServiceMock.Verify(
            l => l.GetLoginTicketAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthorizeAsync_should_throw_ARCAServiceException_for_unsupported_document_type()
    {
        // EN: Remittances (91) is a valid enum but not routed to WSFEv1 or WSFEXv1.
        // ES: Remittances (91) es un enum válido pero no se rutea a WSFEv1 ni WSFEXv1.
        validatorMock
            .Setup(v => v.Validate(It.IsAny<BillingDocumentNumberingRequest>()))
            .Returns(new ValidationResult());

        var service = CreateService();

        var act = () => service.AuthorizeAsync(MinimalRequest(BillingDocumentTypeARCAEnum.Remittances));

        var exception = await act.Should().ThrowAsync<ARCAServiceException>();
        exception.Which.Message.Should().Contain("Unsupported document type");
        exception.Which.Message.Should().Contain(nameof(BillingDocumentTypeARCAEnum.Remittances));
    }

    [Fact]
    public async Task AuthorizeAsync_should_call_validator_before_any_external_call()
    {
        validatorMock
            .Setup(v => v.Validate(It.IsAny<BillingDocumentNumberingRequest>()))
            .Returns(new ValidationResult([new ValidationFailure("X", "y")]));

        var service = CreateService();

        await Assert.ThrowsAsync<ARCAValidationException>(
            () => service.AuthorizeAsync(MinimalRequest(BillingDocumentTypeARCAEnum.FA)));

        validatorMock.Verify(
            v => v.Validate(It.IsAny<BillingDocumentNumberingRequest>()),
            Times.Once);
    }

    [Fact]
    public async Task Unsupported_type_message_should_include_the_enum_name()
    {
        // EN: Sanity that the error message helps diagnose which type fell through routing.
        // ES: Asegura que el mensaje de error indique cuál tipo cayó al default del routing.
        validatorMock
            .Setup(v => v.Validate(It.IsAny<BillingDocumentNumberingRequest>()))
            .Returns(new ValidationResult());

        var service = CreateService();
        var request = MinimalRequest(BillingDocumentTypeARCAEnum.Remittances);

        try
        {
            await service.AuthorizeAsync(request);
            Assert.Fail("Expected ARCAServiceException");
        }
        catch (ARCAServiceException ex)
        {
            ex.Message.Should().Contain("Unsupported");
            ex.ErrorCode.Should().BeNull("the unsupported-type guard does not have a numeric ARCA error code");
        }
    }

    // ====================================================================
    // EN: Happy path + error branches exercised through the SOAP wrappers.
    // ES: Happy path + ramas de error ejercitadas vía los wrappers SOAP.
    // ====================================================================

    private static ElRoso.ARCA.Core.LoginTicketResponse FreshTicket() => new()
    {
        Token = "fake-token",
        Sign = "fake-sign",
        ExpirationTime = DateTime.UtcNow.AddHours(-3).AddHours(6),
    };

    private void SetupValid()
    {
        validatorMock
            .Setup(v => v.Validate(It.IsAny<BillingDocumentNumberingRequest>()))
            .Returns(new ValidationResult());
    }

    private void SetupTokenCacheHit()
    {
        tokenCacheMock
            .Setup(t => t.GetAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshTicket());
    }

    [Fact]
    public async Task Domestic_with_CUIT_should_consult_padron_and_return_CAE()
    {
        SetupValid();
        SetupTokenCacheHit();

        padronMock
            .Setup(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PadronPersonaResult { ClientName = "Cliente SA", Persona = Persona(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO) });

        wsfeMock
            .Setup(w => w.GetLastNumberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                             It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(41);

        wsfeMock
            .Setup(w => w.SolicitarCaeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                            It.IsAny<BillingDocumentNumberingRequest>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WsfeCaeResult
            {
                IsApproved = true,
                Cae = "75123456789012",
                CaeExpiration = new DateTime(2026, 5, 23),
            });

        var request = MinimalRequest(BillingDocumentTypeARCAEnum.FA);
        request.Client = new ClientRequest
        {
            DocumentType = DocumentTypeARCAEnum.CUIT,
            DocumentNumber = 30987654321,
        };

        var service = CreateService();
        var response = await service.AuthorizeAsync(request);

        response.Result.Should().BeTrue();
        response.CAE.Should().Be("75123456789012");
        response.BillingDocumentNumber.Should().Be(42); // lastNumber + 1
        response.BillingDocumentBookExpirationDate.Should().Be(new DateTime(2026, 5, 23));

        // EN: Verify padron WAS called (CUIT client) and condition propagated.
        // ES: Verifica que padron fue llamado (cliente CUIT) y la condición propagó.
        padronMock.Verify(p => p.GetPersonaAsync(
            It.IsAny<string>(), It.IsAny<string>(), 20123456789L, 30987654321L, It.IsAny<CancellationToken>()),
            Times.Once);
        request.Client.ClientName.Should().Be("Cliente SA");
        request.Client.Condition.Should().Be(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO);
    }

    [Fact]
    public async Task Domestic_with_SIN_IDENTIFICAR_should_skip_padron_and_set_consumidor_final()
    {
        SetupValid();
        SetupTokenCacheHit();

        wsfeMock
            .Setup(w => w.GetLastNumberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                             It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        wsfeMock
            .Setup(w => w.SolicitarCaeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                            It.IsAny<BillingDocumentNumberingRequest>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WsfeCaeResult
            {
                IsApproved = true,
                Cae = "75100000000000",
                CaeExpiration = new DateTime(2026, 6, 1),
            });

        // EN: Default client in MinimalRequest is SIN_IDENTIFICAR.
        // ES: El cliente default de MinimalRequest es SIN_IDENTIFICAR.
        var request = MinimalRequest(BillingDocumentTypeARCAEnum.FB);

        var service = CreateService();
        var response = await service.AuthorizeAsync(request);

        response.Result.Should().BeTrue();
        response.BillingDocumentNumber.Should().Be(1);

        padronMock.Verify(p => p.GetPersonaAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never, "padron must NOT be consulted for non-CUIT clients");
        request.Client.Condition.Should().Be(VATConditionARCAEnum.CONSUMIDOR_FINAL);
    }

    [Fact]
    public async Task Domestic_with_CUIT_Monotributo_should_set_MONOTRIBUTO_condition()
    {
        SetupValid();
        SetupTokenCacheHit();

        padronMock
            .Setup(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PadronPersonaResult { ClientName = "Juan Pérez", Persona = Persona(VATConditionARCAEnum.MONOTRIBUTO) });

        wsfeMock
            .Setup(w => w.GetLastNumberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                             It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        wsfeMock
            .Setup(w => w.SolicitarCaeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                            It.IsAny<BillingDocumentNumberingRequest>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WsfeCaeResult { IsApproved = true, Cae = "75", CaeExpiration = DateTime.Today });

        var request = MinimalRequest(BillingDocumentTypeARCAEnum.FB);
        request.Client = new ClientRequest { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20111111111 };

        var service = CreateService();
        await service.AuthorizeAsync(request);

        request.Client.Condition.Should().Be(VATConditionARCAEnum.MONOTRIBUTO);
        request.Client.ClientName.Should().Be("Juan Pérez");
    }

    [Fact]
    public async Task Domestic_with_CUIT_should_return_errors_when_padron_fails()
    {
        SetupValid();
        SetupTokenCacheHit();

        padronMock
            .Setup(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PadronPersonaResult
            {
                Errors = ["No existe persona con ese ID - CUIT: 30987654321"],
            });

        var request = MinimalRequest(BillingDocumentTypeARCAEnum.FA);
        request.Client = new ClientRequest { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 30987654321 };

        var service = CreateService();
        var response = await service.AuthorizeAsync(request);

        response.Result.Should().BeFalse();
        response.Errors.Should().ContainSingle(e => e.Contains("No existe persona"));
        // EN: WSFE must NOT be called when padron fails.
        // ES: WSFE NO debe ser llamado cuando padron falla.
        wsfeMock.Verify(w => w.GetLastNumberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                                  It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                        Times.Never);
    }

    private static PadronPersonaResponse Persona(VATConditionARCAEnum? condition) => new()
    {
        Found = true,
        VATCondition = condition,
    };

    private void SetupApprovedCae()
    {
        wsfeMock
            .Setup(w => w.GetLastNumberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                             It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        wsfeMock
            .Setup(w => w.SolicitarCaeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                            It.IsAny<BillingDocumentNumberingRequest>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WsfeCaeResult { IsApproved = true, Cae = "75", CaeExpiration = DateTime.Today });
    }

    private void SetupPadron(VATConditionARCAEnum? condition) =>
        padronMock
            .Setup(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PadronPersonaResult { ClientName = "Cliente", Persona = Persona(condition) });

    private static BillingDocumentNumberingRequest CuitRequest(BillingDocumentTypeARCAEnum type)
    {
        var request = MinimalRequest(type);
        request.Client = new ClientRequest { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 30711111111 };
        return request;
    }

    [Theory]
    [InlineData(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO)]
    [InlineData(VATConditionARCAEnum.IVA_SUJETO_EXENTO)]
    [InlineData(VATConditionARCAEnum.MONOTRIBUTO)]
    public async Task Domestic_with_CUIT_should_use_the_condition_derived_by_the_padron(VATConditionARCAEnum padronCondition)
    {
        SetupValid();
        SetupTokenCacheHit();
        SetupPadron(padronCondition);
        SetupApprovedCae();
        var request = CuitRequest(BillingDocumentTypeARCAEnum.FB);
        request.Client.SetCondition(VATConditionARCAEnum.CONSUMIDOR_FINAL);

        var response = await CreateService().AuthorizeAsync(request);

        response.Result.Should().BeTrue();
        request.Client.Condition.Should().Be(padronCondition);
        wsfeMock.Verify(w => w.SolicitarCaeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                                 It.Is<BillingDocumentNumberingRequest>(r => r.Client.Condition == padronCondition),
                                                 It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(VATConditionARCAEnum.IVA_NO_ALCANZADO)]
    [InlineData(VATConditionARCAEnum.MONOTRIBUTISTA_SOCIAL)]
    [InlineData(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO)]
    public async Task Undeterminable_padron_should_keep_the_condition_sent_by_the_consumer(VATConditionARCAEnum consumerCondition)
    {
        SetupValid();
        SetupTokenCacheHit();
        SetupPadron(null);
        SetupApprovedCae();
        var request = CuitRequest(BillingDocumentTypeARCAEnum.FB);
        request.Client.SetCondition(consumerCondition);

        var response = await CreateService().AuthorizeAsync(request);

        response.Result.Should().BeTrue();
        request.Client.Condition.Should().Be(consumerCondition);
    }

    [Fact]
    public async Task Padron_with_only_tax_34_should_keep_the_condition_sent_by_the_consumer()
    {
        SetupValid();
        SetupTokenCacheHit();
        SetupApprovedCae();
        var persona = new Padron.personaReturn
        {
            datosGenerales = new Padron.datosGenerales { tipoPersona = "FISICA", estadoClave = "ACTIVO", apellido = "DEMO", nombre = "ANA" },
            datosRegimenGeneral = new Padron.datosRegimenGeneral
            {
                impuesto = [new Padron.impuesto { idImpuesto = 34, idImpuestoSpecified = true, descripcionImpuesto = "IVA NO ALCANZADO" }],
            },
        };
        padronMock
            .Setup(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PadronPersonaResult { ClientName = "DEMO ANA", Persona = PadronOperations.MapPersona(persona, 27111111111) });
        var request = CuitRequest(BillingDocumentTypeARCAEnum.FB);
        request.Client.SetCondition(VATConditionARCAEnum.IVA_NO_ALCANZADO);

        var response = await CreateService().AuthorizeAsync(request);

        response.Result.Should().BeTrue();
        request.Client.Condition.Should().Be(VATConditionARCAEnum.IVA_NO_ALCANZADO);
    }

    [Fact]
    public async Task Padron_without_taxes_should_keep_the_condition_sent_by_the_consumer()
    {
        SetupValid();
        SetupTokenCacheHit();
        SetupApprovedCae();
        var persona = new Padron.personaReturn
        {
            datosGenerales = new Padron.datosGenerales { tipoPersona = "JURIDICA", estadoClave = "ACTIVO", razonSocial = "EMPRESA DEMO SA" },
            datosRegimenGeneral = new Padron.datosRegimenGeneral(),
        };
        padronMock
            .Setup(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PadronPersonaResult { ClientName = "EMPRESA DEMO SA", Persona = PadronOperations.MapPersona(persona, 30711111111) });
        var request = CuitRequest(BillingDocumentTypeARCAEnum.FA);
        request.Client.SetCondition(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO);

        var response = await CreateService().AuthorizeAsync(request);

        response.Result.Should().BeTrue();
        request.Client.Condition.Should().Be(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO);
        wsfeMock.Verify(w => w.SolicitarCaeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                                 It.Is<BillingDocumentNumberingRequest>(r => r.Client.Condition == VATConditionARCAEnum.RESPONSABLE_INSCRIPTO),
                                                 It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Undeterminable_padron_should_not_assume_responsable_inscripto()
    {
        SetupValid();
        SetupTokenCacheHit();
        SetupPadron(null);
        SetupApprovedCae();
        var request = CuitRequest(BillingDocumentTypeARCAEnum.FA);

        await CreateService().AuthorizeAsync(request);

        request.Client.Condition.Should().Be(default(VATConditionARCAEnum));
    }

    [Fact]
    public async Task Domestic_without_CUIT_should_still_send_final_consumer()
    {
        SetupValid();
        SetupTokenCacheHit();
        SetupApprovedCae();
        var request = MinimalRequest(BillingDocumentTypeARCAEnum.FB);
        request.Client.SetCondition(VATConditionARCAEnum.RESPONSABLE_INSCRIPTO);

        await CreateService().AuthorizeAsync(request);

        request.Client.Condition.Should().Be(VATConditionARCAEnum.CONSUMIDOR_FINAL);
    }

    [Fact]
    public async Task Domestic_with_unknown_CUIT_should_still_throw_ARCAServiceException()
    {
        SetupValid();
        SetupTokenCacheHit();

        padronMock
            .Setup(p => p.GetPersonaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ARCAServiceException(
                "ARCA Padron A5 getPersona failed.",
                new System.ServiceModel.FaultException("No existe persona con ese Id")));

        var request = MinimalRequest(BillingDocumentTypeARCAEnum.FA);
        request.Client = new ClientRequest { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 12345678901 };

        var act = () => CreateService().AuthorizeAsync(request);

        await act.Should().ThrowAsync<ARCAServiceException>();
        wsfeMock.Verify(w => w.SolicitarCaeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                                 It.IsAny<BillingDocumentNumberingRequest>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                        Times.Never);
    }

    [Fact]
    public async Task Domestic_should_return_errors_when_WSFE_rejects()
    {
        SetupValid();
        SetupTokenCacheHit();

        wsfeMock
            .Setup(w => w.GetLastNumberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                             It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        wsfeMock
            .Setup(w => w.SolicitarCaeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                            It.IsAny<BillingDocumentNumberingRequest>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WsfeCaeResult
            {
                IsApproved = false,
                Errors = ["10063: CAE ya autorizado para este comprobante"],
            });

        var request = MinimalRequest(BillingDocumentTypeARCAEnum.FC);

        var service = CreateService();
        var response = await service.AuthorizeAsync(request);

        response.Result.Should().BeFalse();
        response.CAE.Should().BeNull();
        response.Errors.Should().ContainSingle(e => e.Contains("10063"));
    }

    [Fact]
    public async Task Export_should_call_wsfex_and_return_CAE()
    {
        SetupValid();
        SetupTokenCacheHit();

        wsfexMock
            .Setup(w => w.GetLastNumberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                             It.IsAny<short>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100L);

        wsfexMock
            .Setup(w => w.AuthorizeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                         It.IsAny<BillingDocumentNumberingRequest>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WsfexCaeResult
            {
                IsApproved = true,
                Cae = "75999999999999",
                DocumentNumber = 101,
                CaeExpiration = new DateTime(2026, 7, 1),
            });

        var request = MinimalRequest(BillingDocumentTypeARCAEnum.InvoiceExport);
        request.BillingDocumentId = 1;
        request.Items = [new ItemRequest { ItemDescription = "Software", Amount = 500 }];
        request.Currency = "Dolares";
        request.Client.ClientLanguage = "Inglés";

        var service = CreateService();
        var response = await service.AuthorizeAsync(request);

        response.Result.Should().BeTrue();
        response.CAE.Should().Be("75999999999999");
        response.BillingDocumentNumber.Should().Be(101);
        response.BillingDocumentBookExpirationDate.Should().Be(new DateTime(2026, 7, 1));

        // EN: WSFE must NOT be called for export documents.
        // ES: WSFE NO debe ser llamado para comprobantes de exportación.
        wsfeMock.VerifyNoOtherCalls();
        padronMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Export_should_return_errors_when_wsfex_rejects()
    {
        SetupValid();
        SetupTokenCacheHit();

        wsfexMock
            .Setup(w => w.GetLastNumberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                             It.IsAny<short>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);

        wsfexMock
            .Setup(w => w.AuthorizeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                         It.IsAny<BillingDocumentNumberingRequest>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WsfexCaeResult
            {
                IsApproved = false,
                Errors = ["1500: Permission denied"],
            });

        var request = MinimalRequest(BillingDocumentTypeARCAEnum.CreditNoteExport);
        request.BillingDocumentId = 1;
        request.Items = [new ItemRequest { ItemDescription = "Refund", Amount = 100 }];

        var service = CreateService();
        var response = await service.AuthorizeAsync(request);

        response.Result.Should().BeFalse();
        response.Errors.Should().ContainSingle(e => e.Contains("1500"));
    }

    [Fact]
    public async Task Token_cache_miss_should_request_fresh_ticket_from_WSAA()
    {
        SetupValid();

        // EN: Cache returns null → service must call ILoginTicketService to get a fresh ticket.
        // ES: Caché devuelve null → service debe llamar a ILoginTicketService por uno fresco.
        tokenCacheMock
            .Setup(t => t.GetAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ElRoso.ARCA.Core.LoginTicketResponse?)null);

        loginTicketServiceMock
            .Setup(l => l.GetLoginTicketAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreshTicket());

        wsfeMock
            .Setup(w => w.GetLastNumberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                             It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        wsfeMock
            .Setup(w => w.SolicitarCaeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                            It.IsAny<BillingDocumentNumberingRequest>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WsfeCaeResult { IsApproved = true, Cae = "75", CaeExpiration = DateTime.Today });

        var service = CreateService();
        await service.AuthorizeAsync(MinimalRequest(BillingDocumentTypeARCAEnum.FB));

        loginTicketServiceMock.Verify(l => l.GetLoginTicketAsync(
            "wsfe", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // EN: Fresh ticket must be persisted into the cache.
        // ES: Ticket fresco debe persistirse en el caché.
        tokenCacheMock.Verify(t => t.SetAsync(
            "wsfe", It.IsAny<long>(), It.IsAny<ElRoso.ARCA.Core.LoginTicketResponse>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ------------------------------------------------------------------ //
    // Query side — reconciliation of already-authorized vouchers
    // EN: These exist because ARCA grants the CAE BEFORE the caller persists it. If the local
    //     save dies in between, re-authorizing issues a SECOND CAE for the same sale — a real
    //     fiscal duplicate. Querying first is the only way to tell the two apart.
    // ES: Existen porque ARCA otorga el CAE ANTES de que el llamador lo persista. Si el guardado
    //     local muere en el medio, re-autorizar emite un SEGUNDO CAE por la misma venta — un
    //     duplicado fiscal real. Consultar primero es la única forma de distinguirlos.
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task GetLastAuthorizedNumber_should_return_what_ARCA_reports()
    {
        SetupTokenCacheHit();

        wsfeMock
            .Setup(w => w.GetLastNumberAsync(It.IsAny<string>(), It.IsAny<string>(), 20123456789L,
                                             (int)BillingDocumentTypeARCAEnum.FC, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(158);

        var service = CreateService();
        var last = await service.GetLastAuthorizedNumberAsync(
            new IssuingCompanyRequest { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
            BillingDocumentTypeARCAEnum.FC,
            3);

        last.Should().Be(158);
    }

    [Fact]
    public async Task GetAuthorized_should_map_the_CAE_so_it_can_be_reconciled_locally()
    {
        SetupTokenCacheHit();

        wsfeMock
            .Setup(w => w.ConsultarComprobanteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(),
                                                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WsfeVoucherResult
            {
                IsApproved = true,
                Cae = "86305060629012",
                CaeExpiration = new DateTime(2026, 8, 2),
                ProcessedDate = new DateTime(2026, 7, 23, 10, 39, 49),
                BookPrefix = 3,
                DocumentType = (int)BillingDocumentTypeARCAEnum.FC,
            });

        var service = CreateService();
        var found = await service.GetAuthorizedAsync(
            new IssuingCompanyRequest { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
            BillingDocumentTypeARCAEnum.FC,
            3,
            157);

        found.IsApproved.Should().BeTrue();
        found.CAE.Should().Be("86305060629012");
        found.CAEExpirationDate.Should().Be(new DateTime(2026, 8, 2));
        found.ProcessedDate.Should().Be(new DateTime(2026, 7, 23, 10, 39, 49));
        found.BillingDocumentNumber.Should().Be(157);
        found.BillingDocumentBookPrefix.Should().Be(3);
        found.BillingDocumentType.Should().Be(BillingDocumentTypeARCAEnum.FC);
        found.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAuthorized_should_surface_ARCA_errors_instead_of_pretending_it_is_approved()
    {
        SetupTokenCacheHit();

        // EN: Querying a voucher that was never authorized comes back as an ARCA error, not an
        //     exception. It must NOT look like an approved voucher with a null CAE.
        // ES: Consultar un comprobante nunca autorizado vuelve como error de ARCA, no como
        //     excepción. NO debe parecer un comprobante aprobado con CAE nulo.
        wsfeMock
            .Setup(w => w.ConsultarComprobanteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(),
                                                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WsfeVoucherResult
            {
                IsApproved = false,
                Errors = ["602: Sin Resultados"],
            });

        var service = CreateService();
        var found = await service.GetAuthorizedAsync(
            new IssuingCompanyRequest { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
            BillingDocumentTypeARCAEnum.FC,
            3,
            9999);

        found.IsApproved.Should().BeFalse();
        found.CAE.Should().BeNull();
        found.Errors.Should().ContainSingle().Which.Should().Contain("Sin Resultados");
    }

    [Fact]
    public async Task GetAuthorized_should_keep_the_point_of_sale_ARCA_actually_returned()
    {
        SetupTokenCacheHit();

        // EN: If ARCA answers with a different point of sale / type than requested, that
        //     mismatch must reach the caller — silently echoing the request would hide it.
        // ES: Si ARCA responde con un punto de venta / tipo distinto al pedido, esa discrepancia
        //     tiene que llegar al llamador — devolver lo pedido en silencio la taparía.
        wsfeMock
            .Setup(w => w.ConsultarComprobanteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                                                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(),
                                                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WsfeVoucherResult
            {
                IsApproved = true,
                Cae = "86305060629012",
                BookPrefix = 4,
                DocumentType = (int)BillingDocumentTypeARCAEnum.NCC,
            });

        var service = CreateService();
        var found = await service.GetAuthorizedAsync(
            new IssuingCompanyRequest { DocumentType = DocumentTypeARCAEnum.CUIT, DocumentNumber = 20123456789 },
            BillingDocumentTypeARCAEnum.FC,
            3,
            157);

        found.BillingDocumentBookPrefix.Should().Be(4);
        found.BillingDocumentType.Should().Be(BillingDocumentTypeARCAEnum.NCC);
    }

    [Fact]
    public async Task Query_methods_should_reject_a_null_issuing_company()
    {
        var service = CreateService();

        await FluentActions
            .Awaiting(() => service.GetLastAuthorizedNumberAsync(null!, BillingDocumentTypeARCAEnum.FC, 3))
            .Should().ThrowAsync<ArgumentNullException>();

        await FluentActions
            .Awaiting(() => service.GetAuthorizedAsync(null!, BillingDocumentTypeARCAEnum.FC, 3, 157))
            .Should().ThrowAsync<ArgumentNullException>();
    }
}
