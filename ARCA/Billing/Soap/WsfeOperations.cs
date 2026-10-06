// <copyright file="WsfeOperations.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using System.Globalization;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;
using Microsoft.Extensions.Logging;

internal sealed class WsfeOperations : IWsfeOperations
{
    private readonly ARCAOptions options;
    private readonly ILogger<WsfeOperations> logger;

    public WsfeOperations(ARCAOptions options, ILogger<WsfeOperations> logger)
    {
        this.options = options;
        this.logger = logger;
    }

    public async Task<int> GetLastNumberAsync(
        string sign,
        string token,
        long cuit,
        int docType,
        int bookPrefix,
        CancellationToken ct)
    {
        // EN: A caller that already walked away gets no socket opened on its behalf.
        // ES: A un llamador que ya se fue no se le abre ningún socket.
        ct.ThrowIfCancellationRequested();

        try
        {
            var client = CreateClient();
            var result = await SoapInvoker.InvokeAsync(
                client,
                () => client.FECompUltimoAutorizadoAsync(new WSFEv1.FECompUltimoAutorizadoRequest
                {
                    Body = new WSFEv1.FECompUltimoAutorizadoRequestBody
                    {
                        Auth = new WSFEv1.FEAuthRequest { Sign = sign, Token = token, Cuit = cuit },
                        CbteTipo = docType,
                        PtoVta = bookPrefix,
                    },
                }),
                ct);
            return result.Body.FECompUltimoAutorizadoResult.CbteNro;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ARCAServiceException("ARCA WSFEv1 FECompUltimoAutorizado failed.", ex);
        }
    }

    public async Task<WsfeCaeResult> SolicitarCaeAsync(
        string sign,
        string token,
        long cuit,
        BillingDocumentNumberingRequest doc,
        int next,
        CancellationToken ct)
    {
        // EN: A caller that already walked away gets no socket opened on its behalf.
        // ES: A un llamador que ya se fue no se le abre ningún socket.
        ct.ThrowIfCancellationRequested();

        var auth = new WSFEv1.FEAuthRequest { Sign = sign, Token = token, Cuit = cuit };
        var isTypeWithVAT = doc.BillingDocumentType is
            BillingDocumentTypeARCAEnum.FA or BillingDocumentTypeARCAEnum.NDA or BillingDocumentTypeARCAEnum.NCA or
            BillingDocumentTypeARCAEnum.FB or BillingDocumentTypeARCAEnum.NDB or BillingDocumentTypeARCAEnum.NCB;

        var condicionIva = ReceiverConditionCode(doc.Client);

        var body = new WSFEv1.FECAESolicitarRequestBody
        {
            Auth = auth,
            FeCAEReq = new WSFEv1.FECAERequest
            {
                FeCabReq = new WSFEv1.FECAECabRequest
                {
                    CantReg = 1,
                    PtoVta = doc.BillingDocumentBookPrefix,
                    CbteTipo = (int)doc.BillingDocumentType,
                },
                FeDetReq =
                [
                    new()
                    {
                        Concepto = (int)doc.ConceptType,
                        CondicionIVAReceptorId = condicionIva,
                        DocTipo = (int)doc.Client.DocumentType,
                        DocNro = doc.Client.DocumentNumber,
                        CbteDesde = next,
                        CbteHasta = next,
                        CbteFch = doc.BillingDocumentDate.ToString("yyyyMMdd"),
                        CbtesAsoc = doc.BillingDocumentType is
                            BillingDocumentTypeARCAEnum.FA or
                            BillingDocumentTypeARCAEnum.FB or
                            BillingDocumentTypeARCAEnum.FC
                            ? null
                            : [.. doc.BillingDocumentNumberingAssociateds!.Select(x => new WSFEv1.CbteAsoc
                            {
                                CbteFch = x.BillingDocumentDate.ToString("yyyyMMdd"),
                                Nro = x.BillingDocumentNumber,
                                PtoVta = x.BillingDocumentBookPrefix,
                                Tipo = (int)x.BillingDocumentType,
                            })],
                        ImpTotal = Math.Round(doc.Total, 2),
                        ImpTotConc = isTypeWithVAT ? Math.Round(doc.AmountNotTax, 2) : 0,
                        ImpNeto = Math.Round(doc.AmountTax, 2),
                        ImpOpEx = 0,
                        ImpTrib = Math.Round(doc.BillingDocumentNumberingOtherTaxAmount, 2),
                        ImpIVA = isTypeWithVAT ? Math.Round(doc.BillingDocumentNumberingTaxAmount, 2) : 0,
                        FchServDesde = doc.ConceptType == ConceptTypeARCAEnum.Products ? string.Empty : doc.DateOfServicesFrom?.ToString("yyyyMMdd"),
                        FchServHasta = doc.ConceptType == ConceptTypeARCAEnum.Products ? string.Empty : doc.DateOfServicesTo?.ToString("yyyyMMdd"),
                        FchVtoPago = doc.ConceptType == ConceptTypeARCAEnum.Products ? string.Empty : doc.PaymentDue?.ToString("yyyyMMdd"),
                        MonId = DictionariesCommon.Currencies[doc.Currency],
                        MonCotiz = Math.Round(doc.ExchangeRate, 2),
                        Iva = isTypeWithVAT
                            ? [.. doc.BillingDocumentNumberingTaxes!.Select(x => new WSFEv1.AlicIva
                                {
                                    Id = DictionariesCommon.Tax[x.PercentageTax],
                                    BaseImp = Math.Round(x.BaseAmount, 2),
                                    Importe = Math.Round(x.Amount, 2),
                                })]
                            : null,
                        Tributos = doc.BillingDocumentNumberingOtherTaxes?
                            .Select(x => new WSFEv1.Tributo
                            {
                                Id = DictionariesCommon.OtherTax[x.Description],
                                Desc = x.Description,
                                BaseImp = Math.Round(x.BaseAmount, 2),
                                Alic = Math.Round(x.PercentageTax, 2),
                                Importe = Math.Round(x.Amount, 2),
                            }).ToArray(),
                    },
                ],
            },
        };

        try
        {
            var client = CreateClient();
            var result = await SoapInvoker.InvokeAsync(
                client,
                () => client.FECAESolicitarAsync(new WSFEv1.FECAESolicitarRequest { Body = body }),
                ct);
            var wsResult = result.Body.FECAESolicitarResult;

            if (wsResult.Errors != null)
            {
                var errors = wsResult.Errors.Select(e => $"{e.Code}: {e.Msg}").ToList();
                return new WsfeCaeResult { IsApproved = false, Errors = errors };
            }

            // Guard against malformed responses with missing or empty FeDetResp.
            // Protección contra respuestas malformadas con FeDetResp vacío o nulo.
            if (wsResult.FeDetResp is not { Length: > 0 })
                throw new ARCAServiceException("WSFEv1 returned an empty or null FeDetResp.");

            var det = wsResult.FeDetResp[0];
            if (det.Resultado == "A")
            {
                return new WsfeCaeResult
                {
                    IsApproved = true,
                    Cae = det.CAE,
                    CaeExpiration = DateTime.ParseExact(det.CAEFchVto, "yyyyMMdd", CultureInfo.InvariantCulture),
                };
            }

            // Resultado == "R" — rejected with observations.
            // Resultado == "R" — rechazado con observaciones.
            var rejErrors = wsResult.FeDetResp
                .SelectMany(d => d.Observaciones ?? [])
                .Select(o => $"{o.Code}: {o.Msg}")
                .ToList();
            return new WsfeCaeResult { IsApproved = false, Errors = rejErrors };
        }
        catch (ARCAServiceException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // EN: Aborting the channel stops the local wait, not the authorization. ARCA may have
            //     issued a CAE for this voucher with nobody left to persist it — log enough to
            //     find it from back-office reconciliation.
            // ES: Abortar el canal corta la espera local, no la autorización. ARCA puede haber
            //     emitido un CAE para este comprobante sin nadie que lo persista — se deja traza
            //     suficiente para encontrarlo desde la reconciliación de back-office.
            logger.LogWarning(
                "CAE request abandoned by the caller for CUIT {Cuit}, type {DocumentType}, point of sale {BookPrefix}, number {Number}. ARCA may have authorized it — reconcile before re-issuing.",
                cuit,
                (int)doc.BillingDocumentType,
                doc.BillingDocumentBookPrefix,
                next);
            throw;
        }
        catch (Exception ex)
        {
            throw new ARCAServiceException("ARCA WSFEv1 FECAESolicitar failed.", ex);
        }
    }

    public async Task<WsfeVoucherResult> ConsultarComprobanteAsync(
        string sign,
        string token,
        long cuit,
        int docType,
        int bookPrefix,
        long number,
        CancellationToken ct)
    {
        // EN: A caller that already walked away gets no socket opened on its behalf.
        // ES: A un llamador que ya se fue no se le abre ningún socket.
        ct.ThrowIfCancellationRequested();

        try
        {
            var client = CreateClient();
            var result = await SoapInvoker.InvokeAsync(
                client,
                () => client.FECompConsultarAsync(new WSFEv1.FECompConsultarRequest
                {
                    Body = new WSFEv1.FECompConsultarRequestBody
                    {
                        Auth = new WSFEv1.FEAuthRequest { Sign = sign, Token = token, Cuit = cuit },
                        FeCompConsReq = new WSFEv1.FECompConsultaReq
                        {
                            CbteTipo = docType,
                            PtoVta = bookPrefix,
                            CbteNro = number,
                        },
                    },
                }),
                ct);

            var wsResult = result.Body.FECompConsultarResult;

            // EN: A query for a voucher that does not exist comes back as an ARCA error, not as
            //     an exception — surface it as errors so the caller can tell "not authorized"
            //     apart from "the call failed".
            // ES: Consultar un comprobante inexistente vuelve como error de ARCA, no como
            //     excepción — se expone como errores para que el llamador distinga "no está
            //     autorizado" de "la llamada falló".
            if (wsResult.Errors is { Length: > 0 })
            {
                var errors = wsResult.Errors.Select(e => $"{e.Code}: {e.Msg}").ToList();
                return new WsfeVoucherResult { IsApproved = false, Errors = errors };
            }

            var found = wsResult.ResultGet;
            if (found is null)
                return new WsfeVoucherResult { IsApproved = false };

            var observations = found.Observaciones?
                .Select(o => $"{o.Code}: {o.Msg}")
                .ToList() ?? [];

            return new WsfeVoucherResult
            {
                IsApproved = found.Resultado == "A",
                Cae = found.CodAutorizacion,
                CaeExpiration = TryParseArcaDate(found.FchVto),
                ProcessedDate = TryParseArcaDate(found.FchProceso),
                BookPrefix = found.PtoVta,
                DocumentType = found.CbteTipo,
                Observations = observations,
            };
        }
        catch (ARCAServiceException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ARCAServiceException("ARCA WSFEv1 FECompConsultar failed.", ex);
        }
    }

    // El valor del enum es el código de condición IVA del receptor de ARCA.
    internal static int ReceiverConditionCode(ClientRequest client) =>
        Enum.IsDefined(client.Condition)
            ? (int)client.Condition
            : throw new ARCAValidationException([$"Condición frente al IVA del receptor no válida: {(int)client.Condition}."]);

    /// <summary>
    /// EN: Parses an ARCA date field. Supports both yyyyMMdd and yyyyMMddHHmmss — FchVto comes
    ///     as the former and FchProceso as the latter.
    /// ES: Parsea un campo fecha de ARCA. Soporta yyyyMMdd y yyyyMMddHHmmss — FchVto viene con
    ///     el primero y FchProceso con el segundo.
    /// </summary>
    internal static DateTime? TryParseArcaDate(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;

        if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt;

        return null;
    }

    private WSFEv1.ServiceSoapClient CreateClient()
    {
        var client = new WSFEv1.ServiceSoapClient(
            WSFEv1.ServiceSoapClient.EndpointConfiguration.ServiceSoap12,
            options.WsfeUrl);
        client.InnerChannel.OperationTimeout = TimeSpan.FromSeconds(options.SoapTimeoutSeconds);
        return client;
    }
}