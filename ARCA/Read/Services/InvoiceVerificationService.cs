// <copyright file="InvoiceVerificationService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Read;

using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;
using FluentValidation;
using Microsoft.Extensions.Logging;

internal sealed class InvoiceVerificationService : IInvoiceVerificationService
{
    private readonly ILogger<InvoiceVerificationService> logger;
    private readonly ILoginTicketService loginTicketService;
    private readonly ITokenCache tokenCache;
    private readonly ARCAOptions options;
    private readonly IValidator<InvoiceVerificationRequest> validator;
    private readonly IInvoiceVerificationOperations operations;

    public InvoiceVerificationService(
        ILogger<InvoiceVerificationService> logger,
        ILoginTicketService loginTicketService,
        ITokenCache tokenCache,
        ARCAOptions options,
        IValidator<InvoiceVerificationRequest> validator,
        IInvoiceVerificationOperations operations)
    {
        this.logger = logger;
        this.loginTicketService = loginTicketService;
        this.tokenCache = tokenCache;
        this.options = options;
        this.validator = validator;
        this.operations = operations;
    }

    public async Task<InvoiceVerificationResponse> VerifyAsync(
        InvoiceVerificationRequest request,
        CancellationToken ct = default)
    {
        // Validate locally before hitting the network.
        // Validar localmente antes de tocar la red.
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.ErrorCode}: {e.ErrorMessage}").ToList();
            throw new ARCAValidationException(errors);
        }

        var requesterCuit = request.RequesterCuit ?? request.IssuingCompany.DocumentNumber;
        var ticket = await GetOrRefreshTokenAsync("wscdc", requesterCuit, ct);
        var result = await operations.VerifyAsync(
            ticket.Sign,
            ticket.Token,
            requesterCuit,
            request,
            ct);

        return new InvoiceVerificationResponse
        {
            IsAuthorized = result.IsAuthorized,
            Resultado = result.Resultado,
            ProcessedDate = result.ProcessedDate,
            Observations = [.. result.Observations],
            Errors = [.. result.Errors],
        };
    }

    private async Task<LoginTicketResponse> GetOrRefreshTokenAsync(string service, long companyId, CancellationToken ct)
    {
        var cached = await tokenCache.GetAsync(service, companyId, ct);
        if (cached is not null)
        {
            logger.LogInformation("Token cache hit for {Service}/{CompanyId}.", service, companyId);
            return cached;
        }

        logger.LogInformation("Token cache miss for {Service}/{CompanyId}. Requesting WSAA.", service, companyId);
        var fresh = await loginTicketService.GetLoginTicketAsync(
            service, options.WsaaUrl, options.CertificatePath, options.CertificatePassword, ct);

        await tokenCache.SetAsync(service, companyId, fresh, ct);
        return fresh;
    }
}