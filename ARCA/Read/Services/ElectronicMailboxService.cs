// <copyright file="ElectronicMailboxService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Read;

using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;
using Microsoft.Extensions.Logging;

internal sealed class ElectronicMailboxService : IElectronicMailboxService
{
    // EN: WSAA service identifier for WSCComu. ARCA's WSAA expects "veconsumerws" (the
    //     Connected Service namespace), NOT "wsccomu". Using the wrong name yields
    //     "Servicio informado inexistente" from WSAA.
    // ES: Identificador WSAA del WSCComu. ARCA espera "veconsumerws" (el namespace del
    //     Connected Service), NO "wsccomu". Usar el nombre incorrecto da
    //     "Servicio informado inexistente" desde WSAA.
    private const string WsccomuServiceName = "veconsumerws";

    private readonly ILogger<ElectronicMailboxService> logger;
    private readonly ILoginTicketService loginTicketService;
    private readonly ITokenCache tokenCache;
    private readonly ARCAOptions options;
    private readonly IElectronicMailboxOperations operations;

    public ElectronicMailboxService(
        ILogger<ElectronicMailboxService> logger,
        ILoginTicketService loginTicketService,
        ITokenCache tokenCache,
        ARCAOptions options,
        IElectronicMailboxOperations operations)
    {
        this.logger = logger;
        this.loginTicketService = loginTicketService;
        this.tokenCache = tokenCache;
        this.options = options;
        this.operations = operations;
    }

    public async Task<MailboxQueryResponse> ListAsync(MailboxQueryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.IssuingCompany.DocumentNumber == 0)
            throw new ARCAValidationException(["IssuingCompany.DocumentNumber is required."]);

        var ticket = await GetOrRefreshTokenAsync(request.IssuingCompany.DocumentNumber, ct);
        return await operations.ListAsync(
            ticket.Sign,
            ticket.Token,
            request.IssuingCompany.DocumentNumber,
            request,
            ct);
    }

    public async Task<MailboxConsumeResponse> ConsumeAsync(MailboxConsumeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.IssuingCompany.DocumentNumber == 0)
            throw new ARCAValidationException(["IssuingCompany.DocumentNumber is required."]);
        if (request.MessageId == 0)
            throw new ARCAValidationException(["MessageId is required."]);

        var ticket = await GetOrRefreshTokenAsync(request.IssuingCompany.DocumentNumber, ct);
        return await operations.ConsumeAsync(
            ticket.Sign,
            ticket.Token,
            request.IssuingCompany.DocumentNumber,
            request.MessageId,
            ct);
    }

    private async Task<LoginTicketResponse> GetOrRefreshTokenAsync(long companyId, CancellationToken ct)
    {
        var cached = await tokenCache.GetAsync(WsccomuServiceName, companyId, ct);
        if (cached is not null)
        {
            logger.LogInformation("Token cache hit for {Service}/{CompanyId}.", WsccomuServiceName, companyId);
            return cached;
        }

        logger.LogInformation("Token cache miss for {Service}/{CompanyId}. Requesting WSAA.", WsccomuServiceName, companyId);
        var fresh = await loginTicketService.GetLoginTicketAsync(
            WsccomuServiceName, options.WsaaUrl, options.CertificatePath, options.CertificatePassword, ct);

        await tokenCache.SetAsync(WsccomuServiceName, companyId, fresh, ct);
        return fresh;
    }
}
