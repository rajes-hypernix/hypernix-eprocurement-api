using FluentValidation;
using FSH.Modules.Procurement.Contracts.v1.Invoices;

namespace FSH.Modules.Procurement.Features.v1.Invoices.SubmitInvoice;

public sealed class SubmitInvoiceCommandValidator : AbstractValidator<SubmitInvoiceCommand>
{
    public SubmitInvoiceCommandValidator()
    {
        RuleFor(x => x.PoId).NotEmpty();
        RuleFor(x => x.InvoiceNo).NotEmpty().MaximumLength(100);
        RuleFor(x => x.WhtRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemCode).NotEmpty().MaximumLength(50);
            line.RuleFor(l => l.Qty).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}
