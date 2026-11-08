using FluentValidation;
using Wasla.Application.Features.Doctors;
using Wasla.Domain.Common;
using Wasla.Domain.Families;

namespace Wasla.Application.Features.Families;
internal sealed class ChildOnboardingSubmitValidator : AbstractValidator<SubmitChildOnboardingCommand>
{
    public ChildOnboardingSubmitValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Intake).NotNull();
        RuleFor(x => x.Intake).ChildRules(v => {
            v.RuleFor(i => i.NameAr).NotEmpty().MaximumLength(200); v.RuleFor(i => i.NameEn).MaximumLength(200);
            v.RuleFor(i => i.DateOfBirth).NotEmpty(); v.RuleFor(i => i.Gender).Must(g => g is Gender.Male or Gender.Female);
            v.RuleFor(i => i.RelationToChild).Must(r => r is FamilyMemberRole.Father or FamilyMemberRole.Mother);
            v.RuleFor(i => i.PhoneNumber).MaximumLength(30); v.RuleFor(i => i.PrimaryContactPhone).MaximumLength(30); v.RuleFor(i => i.PrimaryContactNameAr).MaximumLength(200);
        });
        RuleFor(x => x.Evidence).NotNull().Must(e => e is { Count: > 0 and <= 5 });
        RuleForEach(x => x.Evidence).ChildRules(v => v.RuleFor(e => e.DocumentType).IsInEnum());
    }
}
internal sealed class ChildOnboardingReviewValidator : AbstractValidator<ReviewChildOnboardingCommand>
{
    public ChildOnboardingReviewValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty(); RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid);
        RuleFor(x => x.Decision).Must(d => d is ChildOnboardingStatus.Approved or ChildOnboardingStatus.Rejected or ChildOnboardingStatus.ModificationRequested);
        RuleFor(x => x.Reason).MaximumLength(2000).NotEmpty().When(x => x.Decision != ChildOnboardingStatus.Approved);
    }
}
internal sealed class ChildOnboardingResubmitValidator : AbstractValidator<ResubmitChildOnboardingCommand>
{
    public ChildOnboardingResubmitValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty(); RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid);
        RuleFor(x => x.Evidence).NotNull().Must(e => e is { Count: > 0 and <= 5 });
        RuleForEach(x => x.Evidence).ChildRules(v => v.RuleFor(e => e.DocumentType).IsInEnum());
    }
}
internal sealed class OperationalAuthorityRevokeValidator : AbstractValidator<RevokeOperationalAuthorityCommand>
{
    public OperationalAuthorityRevokeValidator()
    {
        RuleFor(x => x.AuthorityId).NotEmpty(); RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RowVersion).NotEmpty().Must(RowVersionCodec.IsValid); RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
