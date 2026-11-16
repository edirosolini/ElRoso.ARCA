// <copyright file="ClientValidator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;
using FluentValidation;

internal class ClientValidator : AbstractValidator<ClientRequest>
{
    public ClientValidator()
    {
        RuleFor(c => c.DocumentType).IsInEnum();
        RuleFor(c => c.DocumentNumber)
            .NotEmpty()
            .When(c => c.DocumentType != DocumentTypeARCAEnum.SIN_IDENTIFICAR);
    }
}
