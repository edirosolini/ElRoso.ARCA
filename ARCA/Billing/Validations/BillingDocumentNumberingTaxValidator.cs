// <copyright file="BillingDocumentNumberingTaxValidator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;
using FluentValidation;

internal class BillingDocumentNumberingTaxValidator : AbstractValidator<BillingDocumentNumberingTaxRequest>
{
    public BillingDocumentNumberingTaxValidator()
    {
        RuleFor(x => x.PercentageTax).Must(p => DictionariesCommon.Tax.ContainsKey(p))
            .WithMessage("'{PropertyValue}' is not a valid ARCA VAT percentage.");
        RuleFor(x => x.BaseAmount).NotEmpty();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
    }
}
