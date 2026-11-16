// <copyright file="ElectronicMailboxOperations.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Read;

using System.Globalization;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;

internal sealed class ElectronicMailboxOperations : IElectronicMailboxOperations
{
    private readonly ARCAOptions options;

    public ElectronicMailboxOperations(ARCAOptions options)
    {
        this.options = options;
    }

    public async Task<MailboxQueryResponse> ListAsync(
        string sign,
        string token,
        long cuitRepresentada,
        MailboxQueryRequest request,
        CancellationToken ct)
    {
        // EN: A caller that already walked away gets no socket opened on its behalf.
        // ES: A un llamador que ya se fue no se le abre ningún socket.
        ct.ThrowIfCancellationRequested();

        try
        {
            var client = CreateClient();

            var auth = new WSCComu.AuthRequest
            {
                token = token,
                sign = sign,
                cuitRepresentada = cuitRepresentada,
            };

            var filter = BuildFilter(request);
            var result = await SoapInvoker.InvokeAsync(
                client,
                () => client.consultarComunicacionesAsync(auth, filter),
                ct);
            var paginada = result.RespuestaPaginada;

            if (paginada is null)
                return new MailboxQueryResponse();

            return new MailboxQueryResponse
            {
                Page = paginada.pagina,
                TotalPages = paginada.totalPaginas,
                ItemsPerPage = paginada.itemsPorPagina,
                TotalItems = paginada.totalItems,
                Messages = [.. (paginada.items ?? []).Select(MapSummary)],
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ARCAServiceException("ARCA WSCComu consultarComunicaciones failed.", ex);
        }
    }

    public async Task<MailboxConsumeResponse> ConsumeAsync(
        string sign,
        string token,
        long cuitRepresentada,
        long messageId,
        CancellationToken ct)
    {
        // EN: A caller that already walked away gets no socket opened on its behalf.
        // ES: A un llamador que ya se fue no se le abre ningún socket.
        ct.ThrowIfCancellationRequested();

        try
        {
            var client = CreateClient();

            var auth = new WSCComu.AuthRequest
            {
                token = token,
                sign = sign,
                cuitRepresentada = cuitRepresentada,
            };

            // EN: incluirAdjuntos=true so the consumer can render the full message in one call.
            // ES: incluirAdjuntos=true para que el consumidor pueda renderizar todo en una llamada.
            var result = await SoapInvoker.InvokeAsync(
                client,
                () => client.consumirComunicacionAsync(auth, messageId, true),
                ct);
            var comunicacion = result.Comunicacion;

            if (comunicacion is null)
            {
                return new MailboxConsumeResponse
                {
                    Success = false,
                    Errors = ["WSCComu returned no message for the given id."],
                };
            }

            return new MailboxConsumeResponse
            {
                Success = true,
                Message = MapMessage(comunicacion),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ARCAServiceException("ARCA WSCComu consumirComunicacion failed.", ex);
        }
    }

    private WSCComu.VEConsumerClient CreateClient()
    {
        // EN: WSCComu in production responds with MTOM (multipart/related; xop+xml) even when
        //     there are no attachments. The default generated binding uses plain SOAP encoding
        //     and throws ProtocolException trying to parse the response.
        //     Force MessageEncoding=Mtom + raise MaxReceivedMessageSize so large attachment
        //     payloads in ConsumeAsync don't choke.
        // ES: WSCComu en producción siempre responde MTOM. El binding default del generated
        //     client espera SOAP plano y explota al parsear. Forzamos Mtom y subimos el
        //     tamaño máximo para que los adjuntos del ConsumeAsync no rompan.
        var binding = new System.ServiceModel.BasicHttpBinding
        {
            MessageEncoding = System.ServiceModel.WSMessageEncoding.Mtom,
            MaxReceivedMessageSize = int.MaxValue,
            SendTimeout = TimeSpan.FromSeconds(options.SoapTimeoutSeconds),
            ReceiveTimeout = TimeSpan.FromSeconds(options.SoapTimeoutSeconds),
            OpenTimeout = TimeSpan.FromSeconds(options.SoapTimeoutSeconds),
            CloseTimeout = TimeSpan.FromSeconds(options.SoapTimeoutSeconds),
        };
        binding.Security.Mode = System.ServiceModel.BasicHttpSecurityMode.Transport;

        var client = new WSCComu.VEConsumerClient(
            binding,
            new System.ServiceModel.EndpointAddress(options.WsccomuUrl));
        client.InnerChannel.OperationTimeout = TimeSpan.FromSeconds(options.SoapTimeoutSeconds);
        return client;
    }

    internal static WSCComu.Filter BuildFilter(MailboxQueryRequest req)
    {
        var f = new WSCComu.Filter
        {
            pagina = req.Page,
            fechaDesde = req.FromDate?.ToString("yyyy-MM-dd"),
            fechaHasta = req.ToDate?.ToString("yyyy-MM-dd"),
            referencia1 = req.Reference1,
            referencia2 = req.Reference2,
        };

        if (req.StateId.HasValue) { f.estado = req.StateId.Value; f.estadoSpecified = true; }
        if (req.HasAttachment.HasValue) { f.tieneAdjunto = req.HasAttachment.Value; f.tieneAdjuntoSpecified = true; }
        if (req.PublishingSystemId.HasValue) { f.sistemaPublicadorId = req.PublishingSystemId.Value; f.sistemaPublicadorIdSpecified = true; }
        if (req.PageSize.HasValue) { f.resultadosPorPagina = req.PageSize.Value; f.resultadosPorPaginaSpecified = true; }
        return f;
    }

    internal static MailboxMessageSummary MapSummary(WSCComu.ComunicacionSimplificada s) => new()
    {
        Id = s.idComunicacion,
        RecipientCuit = s.cuitDestinatario,
        PublishedDate = TryParseDate(s.fechaPublicacion),
        ExpirationDate = TryParseDate(s.fechaVencimiento),
        PublishingSystemId = s.sistemaPublicador,
        PublishingSystem = s.sistemaPublicadorDesc ?? string.Empty,
        StateId = s.estado,
        StateName = s.estadoDesc ?? string.Empty,
        Subject = s.asunto ?? string.Empty,
        Priority = s.prioridad,
        HasAttachment = s.tieneAdjunto,
        Reference1 = s.referencia1,
        Reference2 = s.referencia2,
    };

    internal static MailboxMessage MapMessage(WSCComu.Comunicacion c) => new()
    {
        Id = c.idComunicacion,
        RecipientCuit = c.cuitDestinatario,
        PublishedDate = TryParseDate(c.fechaPublicacion),
        ExpirationDate = TryParseDate(c.fechaVencimiento),
        PublishingSystem = c.sistemaPublicadorDesc ?? string.Empty,
        StateId = c.estado,
        StateName = c.estadoDesc ?? string.Empty,
        Subject = c.asunto ?? string.Empty,
        Body = c.mensaje ?? string.Empty,
        Priority = c.prioridad,
        HasAttachment = c.tieneAdjunto,
        Attachments = [.. (c.adjuntos ?? []).Select(MapAttachment)],
    };

    internal static MailboxAttachment MapAttachment(WSCComu.adjunto a) => new()
    {
        FileName = a.filename ?? string.Empty,
        // EN: WSCComu doesn't return MIME type — caller can infer from filename extension.
        // ES: WSCComu no devuelve MIME type — el caller lo puede inferir de la extensión.
        MimeType = string.Empty,
        Content = a.content ?? [],
    };

    internal static DateTime? TryParseDate(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        // EN: Try several common ARCA date formats.
        // ES: Probar varios formatos comunes de fecha de ARCA.
        string[] formats = ["yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd", "dd/MM/yyyy"];
        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(value, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2) ? dt2 : null;
    }
}
