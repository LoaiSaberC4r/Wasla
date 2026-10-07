using FluentValidation;
using Wasla.Domain.Diagnostics;

namespace Wasla.Application.Features.Diagnostics;

internal sealed class DiagnosticOrderValidator : AbstractValidator<DiagnosticOrderCommand>
{
    public DiagnosticOrderValidator()
    {
        RuleFor(r => r.Kind).IsInEnum(); RuleFor(r => r.Mutation).IsInEnum();
        RuleFor(r => r.PatientInstructions).MaximumLength(2000); RuleFor(r => r.Reason).MaximumLength(1000);
        RuleFor(r => r.Items).Must(items => items is null || items.Count <= 100 && items.All(i => i is not null));
        RuleFor(r => r.Item).Must(i => i is null || !(i.DoctorInstructions?.Length > 2000));
    }
}
internal sealed class DiagnosticResultValidator : AbstractValidator<DiagnosticResultCommand>
{
    public DiagnosticResultValidator()
    {
        RuleFor(r => r.Kind).IsInEnum(); RuleFor(r => r.Mutation).IsInEnum();
        RuleFor(r => r.ExternalProviderName).MaximumLength(500); RuleFor(r => r.PatientNote).MaximumLength(2000);
        RuleFor(r => r.Reason).MaximumLength(1000);
        RuleFor(r => r.Attachments).Must(a => a is null || a.Count <= 20 && a.All(f => f is not null && f.Content is not null));
        RuleFor(r => r.CoveredItemIds).Must(ids => ids is null || ids.Count <= 100);
    }
}
internal sealed class MedicalCatalogValidator : AbstractValidator<MutateMedicalCatalogCommand>
{
    public MedicalCatalogValidator() { RuleFor(r => r.Kind).IsInEnum(); RuleFor(r => r.Mutation).IsInEnum(); RuleFor(r => r.Reason).MaximumLength(1000); }
}
internal sealed class MedicalCatalogRequestValidator : AbstractValidator<MutateMedicalCatalogRequestCommand>
{
    public MedicalCatalogRequestValidator() { RuleFor(r => r.Kind).IsInEnum(); RuleFor(r => r.Mutation).IsInEnum(); RuleFor(r => r.Reason).MaximumLength(1000); }
}
