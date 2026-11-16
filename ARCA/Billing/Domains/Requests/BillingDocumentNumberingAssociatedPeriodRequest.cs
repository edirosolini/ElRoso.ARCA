// <copyright file="BillingDocumentNumberingAssociatedPeriodRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

/// <summary>
/// Date range associated with a credit or debit note. / Período asociado a una nota de crédito o débito.
/// </summary>
public class BillingDocumentNumberingAssociatedPeriodRequest
{
    /// <summary>
    /// Gets or sets the start date of the associated period.
    /// / Obtiene o establece la fecha de inicio del período asociado.
    /// </summary>
    public DateTime DateFrom { get; set; }

    /// <summary>
    /// Gets or sets the end date of the associated period.
    /// / Obtiene o establece la fecha de fin del período asociado.
    /// </summary>
    public DateTime DateTo { get; set; }
}
