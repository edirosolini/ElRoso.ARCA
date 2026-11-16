// <copyright file="ILoginTicketService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Core;

using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;

/// <summary>
/// Obtains a Login Ticket (token + sign) from the ARCA WSAA service.
/// Obtiene un Login Ticket (token + sign) del servicio WSAA de ARCA.
/// </summary>
public interface ILoginTicketService
{
    /// <summary>
    /// Authenticates against WSAA and returns a signed login ticket.
    /// Autentica contra el WSAA y retorna un ticket de sesión firmado.
    /// </summary>
    /// <param name="service">Target ARCA service name (e.g. "wsfe", "wsfex").</param>
    /// <param name="wsaaUrl">WSAA endpoint URL.</param>
    /// <param name="certificatePath">Absolute path to the signing .pfx/.p12 certificate.</param>
    /// <param name="certificatePassword">Certificate password, or null if none.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<LoginTicketResponse> GetLoginTicketAsync(
        string service,
        string wsaaUrl,
        string certificatePath,
        string? certificatePassword,
        CancellationToken ct = default);
}
