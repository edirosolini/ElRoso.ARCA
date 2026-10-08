// <copyright file="InvoiceVerificationValidator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Read;

using ElRoso.ARCA.Core;
using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Read;
using FluentValidation;

internal class InvoiceVerificationValidator : AbstractValidator<InvoiceVerificationRequest>
{
    public InvoiceVerificationValidator()
    {
        RuleFor(x => x.BillingDocumentType).IsInEnum();
        RuleFor(x => x.AuthorizationMode).IsInEnum();
        RuleFor(x => x.BillingDocumentBookPrefix).GreaterThan(0)
            .WithMessage("El punto de venta debe ser mayor a 0.");
        RuleFor(x => x.BillingDocumentNumber).GreaterThan(0)
            .WithMessage("El número de comprobante debe ser mayor a 0.");
        RuleFor(x => x.BillingDocumentDate).NotEmpty()
            .WithMessage("La fecha del comprobante es requerida.");
        RuleFor(x => x.TotalAmount).GreaterThan(0)
            .WithMessage("El importe total debe ser mayor a 0.");
        RuleFor(x => x.AuthorizationCode).NotEmpty()
            .WithMessage("El código de autorización (CAE/CAI/CAEA) es requerido.");
        RuleFor(x => x.IssuingCompany).NotNull();
        RuleFor(x => x.IssuingCompany.DocumentNumber).GreaterThan(0)
            .When(x => x.IssuingCompany is not null)
            .WithMessage("El CUIT del emisor es requerido.");
        RuleFor(x => x.RequesterCuit).GreaterThan(0)
            .When(x => x.RequesterCuit is not null)
            .WithMessage("El CUIT que consulta debe ser mayor a 0.");
    }
}