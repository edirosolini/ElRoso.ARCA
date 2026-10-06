// <copyright file="PadronService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using ElRoso.ARCA.Core;
using Microsoft.Extensions.Logging;

internal sealed class PadronService : IPadronService
{
    private const string ServiceName = "ws_sr_constancia_inscripcion";

    private readonly ILogger<PadronService> logger;
    private readonly ILoginTicketService loginTicketService;
    private readonly ITokenCache tokenCache;
    private readonly ARCAOptions options;
    private readonly IPadronOperations padron;

    public PadronService(
        ILogger<PadronService> logger,
        ILoginTicketService loginTicketService,
        ITokenCache tokenCache,
        ARCAOptions options,
        IPadronOperations padron)
    {
        this.logger = logger;
        this.loginTicketService = loginTicketService;
        this.tokenCache = tokenCache;
        this.options = options;
        this.padron = padron;
    }

    public async Task<PadronPersonaResponse> GetPersonaAsync(long representedCuit, long cuit, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var ticket = await this.GetOrRefreshTokenAsync(ServiceName, representedCuit, ct);
        try
        {
            var result = await this.padron.GetPersonaAsync(ticket.Sign, ticket.Token, representedCuit, cuit, ct);
            return result.Persona;
        }
        catch (ARCAServiceException ex) when (PadronOperations.IsPersonaNotFound(ex))
        {
            return new PadronPersonaResponse
            {
                Found = false,
                Cuit = cuit,
                Errors = [ex.InnerException!.Message],
            };
        }
    }

    private async Task<LoginTicketResponse> GetOrRefreshTokenAsync(string service, long companyId, CancellationToken ct)
    {
        var cached = await this.tokenCache.GetAsync(service, companyId, ct);
        if (cached is not null)
        {
            this.logger.LogInformation("Token cache hit for {Service}/{CompanyId}.", service, companyId);
            return cached;
        }

        this.logger.LogInformation("Token cache miss for {Service}/{CompanyId}. Requesting WSAA.", service, companyId);
        var fresh = await this.loginTicketService.GetLoginTicketAsync(
            service, this.options.WsaaUrl, this.options.CertificatePath, this.options.CertificatePassword, ct);

        await this.tokenCache.SetAsync(service, companyId, fresh, ct);
        return fresh;
    }
}
