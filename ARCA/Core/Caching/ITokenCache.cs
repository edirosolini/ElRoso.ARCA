// <copyright file="ITokenCache.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Core;

using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;

/// <summary>
/// Abstraction for storing and retrieving WSAA login tickets.
/// Abstracción para almacenar y recuperar tokens del WSAA.
/// </summary>
public interface ITokenCache
{
    /// <summary>
    /// Returns a valid (non-expired) ticket, or null if cache miss / expired.
    /// Retorna un ticket válido (no expirado), o null si no existe o expiró.
    /// </summary>
    Task<LoginTicketResponse?> GetAsync(string service, long companyId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new ticket. Overwrites any existing entry for the same service+company.
    /// Guarda un nuevo ticket. Sobreescribe cualquier entrada existente para el mismo servicio+empresa.
    /// </summary>
    Task SetAsync(string service, long companyId, LoginTicketResponse ticket, CancellationToken ct = default);
}
