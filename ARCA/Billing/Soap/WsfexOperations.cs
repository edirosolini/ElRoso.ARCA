// <copyright file="WsfexOperations.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using System.Globalization;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;
using Microsoft.Extensions.Logging;

internal sealed class WsfexOperations : IWsfexOperations
{
    private readonly ARCAOptions options;
    private readonly ILogger<WsfexOperations> logger;

    public WsfexOperations(ARCAOptions options, ILogger<WsfexOperations> logger)
    {
        this.options = options;
        this.logger = logger;
    }

    public async Task<long> GetLastNumberAsync(
        string sign,
        string token,
        long cuit,
        short docType,
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
                () => client.FEXGetLast_CMPAsync(new WSFEXv1.FEXGetLast_CMPRequest
                {
                    Auth = new WSFEXv1.ClsFEX_LastCMP
                    {
                        Token = token,
                        Sign = sign,
                        Cuit = cuit,
                        Cbte_Tipo = docType,
                        Pto_venta = bookPrefix,
                    },
                }),
                ct);
            return result.FEXGetLast_CMPResult.FEXResult_LastCMP.Cbte_nro;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ARCAServiceException("ARCA WSFEXv1 FEXGetLast_CMP failed.", ex);
        }
    }

    public async Task<WsfexCaeResult> AuthorizeAsync(
        string sign,
        string token,
        long cuit,
        BillingDocumentNumberingRequest doc,
        long next,
        CancellationToken ct)
    {
        // EN: A caller that already walked away gets no socket opened on its behalf.
        // ES: A un llamador que ya se fue no se le abre ningún socket.
        ct.ThrowIfCancellationRequested();

        var auth = new WSFEXv1.ClsFEXAuthRequest { Sign = sign, Token = token, Cuit = cuit };

        var request = new WSFEXv1.FEXAuthorizeRequest
        {
            Auth = auth,
            Cmp = new WSFEXv1.ClsFEXRequest
            {
                Id = (long)doc.BillingDocumentId!,
                Cbte_Tipo = (short)(int)doc.BillingDocumentType,
                Fecha_cbte = doc.BillingDocumentDate.ToString("yyyyMMdd"),
                Punto_vta = doc.BillingDocumentBookPrefix,
                Cbte_nro = next,
                Tipo_expo = (short)doc.ConceptType,
                Permiso_existente = string.Empty,
                Dst_cmp = doc.Client.CountryId,
                Cliente = doc.Client.ClientName,
                Cuit_pais_cliente = doc.Client.DocumentNumber,
                Domicilio_cliente = doc.Client.Address,
                Moneda_Id = DictionariesCommon.Currencies[doc.Currency],
                Moneda_ctz = (decimal)doc.ExchangeRate,
                Imp_total = (decimal)doc.Total,
                Idioma_cbte = DictionariesCommon.Language[doc.Client.ClientLanguage],
                Items =
                [
                    .. doc.Items!.Select(i => new WSFEXv1.Item
                    {
                        Pro_ds = i.ItemDescription,
                        Pro_umed = 0,
                        Pro_total_item = (decimal)i.Amount,
                    }),
                ],
                Fecha_pago = doc.ConceptType == ConceptTypeARCAEnum.Products
                    ? string.Empty
                    : doc.PaymentDue?.ToString("yyyyMMdd"),
            },
        };

        try
        {
            var client = CreateClient();
            var result = await SoapInvoker.InvokeAsync(client, () => client.FEXAuthorizeAsync(request), ct);
            var fexResult = result.FEXAuthorizeResult;

            if (fexResult.FEXErr.ErrCode != 0)
            {
                return new WsfexCaeResult
                {
                    IsApproved = false,
                    Errors = [$"{fexResult.FEXErr.ErrCode}: {fexResult.FEXErr.ErrMsg}"],
                };
            }

            return new WsfexCaeResult
            {
                IsApproved = true,
                Cae = fexResult.FEXResultAuth.Cae,
                CaeExpiration = DateTime.ParseExact(fexResult.FEXResultAuth.Fch_venc_Cae, "yyyyMMdd", CultureInfo.InvariantCulture),
                DocumentNumber = (int)fexResult.FEXResultAuth.Cbte_nro,
            };
        }
        catch (ARCAServiceException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // EN: Same caveat as WSFEv1 — the CAE may exist at ARCA with nobody left to persist it.
            // ES: Misma salvedad que en WSFEv1 — el CAE puede existir en ARCA sin nadie que lo persista.
            logger.LogWarning(
                "Export CAE request abandoned by the caller for CUIT {Cuit}, type {DocumentType}, point of sale {BookPrefix}, number {Number}. ARCA may have authorized it — reconcile before re-issuing.",
                cuit,
                (int)doc.BillingDocumentType,
                doc.BillingDocumentBookPrefix,
                next);
            throw;
        }
        catch (Exception ex)
        {
            throw new ARCAServiceException("ARCA WSFEXv1 FEXAuthorize failed.", ex);
        }
    }

    private WSFEXv1.ServiceSoapClient CreateClient()
    {
        var client = new WSFEXv1.ServiceSoapClient(
            WSFEXv1.ServiceSoapClient.EndpointConfiguration.ServiceSoap12,
            options.WsfexUrl);
        client.InnerChannel.OperationTimeout = TimeSpan.FromSeconds(options.SoapTimeoutSeconds);
        return client;
    }
}
