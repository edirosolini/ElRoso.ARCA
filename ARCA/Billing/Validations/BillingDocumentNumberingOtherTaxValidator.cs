// <copyright file="BillingDocumentNumberingOtherTaxValidator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;
using FluentValidation;

internal class BillingDocumentNumberingOtherTaxValidator : AbstractValidator<BillingDocumentNumberingOtherTaxRequest>
{
    public BillingDocumentNumberingOtherTaxValidator()
    {
        RuleFor(x => x.Description).Must(d => DictionariesCommon.OtherTax.ContainsKey(d))
            .WithMessage("'{PropertyValue}' is not a valid ARCA other-tax description.");
        RuleFor(x => x.BaseAmount).NotEmpty();
        RuleFor(x => x.PercentageTax).NotEmpty();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
    }
}
