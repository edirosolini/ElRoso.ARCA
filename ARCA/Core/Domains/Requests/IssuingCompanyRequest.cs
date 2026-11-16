// <copyright file="IssuingCompanyRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Core;

using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;

public class IssuingCompanyRequest
{
    public DocumentTypeARCAEnum DocumentType { get; set; }

    public long DocumentNumber { get; set; }

    public VATConditionARCAEnum VATCondition { get; set; }
}
