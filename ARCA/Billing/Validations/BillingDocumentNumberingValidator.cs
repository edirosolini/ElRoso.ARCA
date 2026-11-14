// <copyright file="BillingDocumentNumberingValidator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

using ElRoso.ARCA.Billing;
using ElRoso.ARCA.Core;
using ElRoso.ARCA.Read;
using FluentValidation;

internal class BillingDocumentNumberingValidator : AbstractValidator<BillingDocumentNumberingRequest>
{
    private static readonly DateTime MinimumAssociatedPeriodDate = new(2006, 1, 1);

    private static readonly BillingDocumentTypeARCAEnum[] NoteTypes =
    [
        BillingDocumentTypeARCAEnum.NDA, BillingDocumentTypeARCAEnum.NCA,
        BillingDocumentTypeARCAEnum.NDB, BillingDocumentTypeARCAEnum.NCB,
        BillingDocumentTypeARCAEnum.NDC, BillingDocumentTypeARCAEnum.NCC,
    ];

    private static readonly BillingDocumentTypeARCAEnum[] ExportTypes =
    [
        BillingDocumentTypeARCAEnum.InvoiceExport,
        BillingDocumentTypeARCAEnum.DebitNoteExport,
        BillingDocumentTypeARCAEnum.CreditNoteExport,
    ];

    private static readonly BillingDocumentTypeARCAEnum[] VatTypes =
    [
        BillingDocumentTypeARCAEnum.FA, BillingDocumentTypeARCAEnum.NDA, BillingDocumentTypeARCAEnum.NCA,
        BillingDocumentTypeARCAEnum.FB, BillingDocumentTypeARCAEnum.NDB, BillingDocumentTypeARCAEnum.NCB,
    ];

    public BillingDocumentNumberingValidator()
    {
        RuleFor(x => x.BillingDocumentType).IsInEnum();
        RuleFor(x => x.ConceptType).IsInEnum();
        RuleFor(x => x.BillingDocumentBookPrefix).NotEmpty();
        RuleFor(x => x.BillingDocumentDate).NotEmpty();
        RuleFor(x => x.ExchangeRate).NotEmpty();
        RuleFor(x => x.AmountTax).NotEmpty();
        RuleFor(x => x.IssuingCompany).NotNull();
        RuleFor(x => x.Client).NotNull().SetValidator(new ClientValidator());
        RuleFor(x => x.Currency).Must(c => DictionariesCommon.Currencies.ContainsKey(c))
            .WithMessage("'{PropertyValue}' is not a supported currency.");

        // Credit/Debit notes must include linked documents or an associated period.
        // Las notas de crédito/débito deben incluir comprobantes asociados o un período asociado.
        RuleFor(x => x)
            .Must(HaveAssociatedDocumentsOrPeriod)
            .When(x => NoteTypes.Contains(x.BillingDocumentType))
            .WithName(nameof(BillingDocumentNumberingRequest.BillingDocumentNumberingAssociateds))
            .WithMessage("Las notas de crédito y débito deben incluir comprobantes asociados o un período asociado.");

        RuleFor(x => x.BillingDocumentNumberingAssociatedPeriod)
            .Null()
            .Unless(x => NoteTypes.Contains(x.BillingDocumentType))
            .WithMessage("El período asociado solo puede informarse en notas de crédito o débito.");

        RuleFor(x => x.BillingDocumentNumberingAssociatedPeriod)
            .Must(BeValidAssociatedPeriod)
            .WithMessage("El período asociado debe tener fechas posteriores al 01/01/2006 y DateFrom no puede ser posterior a DateTo.");

        RuleFor(x => x)
            .Must(x => x.BillingDocumentNumberingAssociatedPeriod is null ||
                       x.BillingDocumentNumberingAssociatedPeriod.DateTo.Date <= x.BillingDocumentDate.Date)
            .WithName(nameof(BillingDocumentNumberingRequest.BillingDocumentNumberingAssociatedPeriod))
            .WithMessage("DateTo del período asociado no puede ser posterior a la fecha de emisión del comprobante.");

        // Export documents require an ID and at least one item.
        // Los documentos de exportación requieren ID e ítems.
        RuleFor(x => x.BillingDocumentId).NotNull().NotEmpty()
            .When(x => ExportTypes.Contains(x.BillingDocumentType));
        RuleFor(x => x.Items).NotNull().NotEmpty()
            .When(x => ExportTypes.Contains(x.BillingDocumentType));

        // Service concept requires date range and payment due.
        // El concepto Servicios requiere rango de fechas y vencimiento.
        RuleFor(x => x)
            .Must(BeValidServiceDates)
            .When(x => x.ConceptType == ConceptTypeARCAEnum.Services)
            .WithMessage("Service invoices require DateOfServicesFrom, DateOfServicesTo, and PaymentDue.");

        RuleForEach(x => x.BillingDocumentNumberingTaxes)
            .SetValidator(new BillingDocumentNumberingTaxValidator())
            .When(x => VatTypes.Contains(x.BillingDocumentType));

        RuleForEach(x => x.BillingDocumentNumberingOtherTaxes)
            .SetValidator(new BillingDocumentNumberingOtherTaxValidator());
    }

    private static bool BeValidServiceDates(BillingDocumentNumberingRequest x)
    {
        // Export services only need PaymentDue.
        // Exportación de servicios solo requiere PaymentDue.
        if (ExportTypes.Contains(x.BillingDocumentType))
            return x.PaymentDue != null;

        return x.DateOfServicesFrom != null && x.DateOfServicesTo != null && x.PaymentDue != null;
    }

    private static bool HaveAssociatedDocumentsOrPeriod(BillingDocumentNumberingRequest request)
    {
        return request.BillingDocumentNumberingAssociateds?.Any() == true ||
               request.BillingDocumentNumberingAssociatedPeriod is not null;
    }

    private static bool BeValidAssociatedPeriod(BillingDocumentNumberingAssociatedPeriodRequest? period)
    {
        return period is null || (period.DateFrom.Date > MinimumAssociatedPeriodDate &&
                                  period.DateTo.Date > MinimumAssociatedPeriodDate &&
                                  period.DateFrom.Date <= period.DateTo.Date);
    }
}
