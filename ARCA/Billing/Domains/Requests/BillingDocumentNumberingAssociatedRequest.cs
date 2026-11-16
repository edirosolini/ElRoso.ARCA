// <copyright file="BillingDocumentNumberingAssociatedRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;

public class BillingDocumentNumberingAssociatedRequest
{
    public int BillingDocumentBookPrefix { get; set; }

    public DateTime BillingDocumentDate { get; set; }

    public long BillingDocumentNumber { get; set; }

    public BillingDocumentTypeARCAEnum BillingDocumentType { get; set; }
}
