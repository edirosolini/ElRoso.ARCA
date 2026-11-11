// <copyright file="PointOfSaleResponse.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing
{
    /// <summary>
    /// Punto de venta habilitado para webservices, tal como lo devuelve WSFEv1 FEParamGetPtosVenta.
    /// </summary>
    public sealed record PointOfSaleResponse
    {
        /// <summary>
        /// Gets the point of sale number. / Número del punto de venta.
        /// </summary>
        public int Number { get; init; }

        /// <summary>
        /// Gets the emission type reported by ARCA. / Tipo de emisión informado por ARCA (por ejemplo "CAE - RECE" o "CAEA - RECE").
        /// </summary>
        public string EmissionType { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether ARCA reports it blocked (Bloqueado == "S").
        /// </summary>
        public bool IsBlocked { get; init; }

        /// <summary>
        /// Gets the deactivation date; null while it is still active. / Fecha de baja; null si sigue vigente.
        /// </summary>
        public DateTime? DeactivationDate { get; init; }
    }
}
