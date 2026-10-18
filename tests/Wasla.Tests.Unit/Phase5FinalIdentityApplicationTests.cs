using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application;
using Wasla.Application.Features.PatientOnboarding;
using Wasla.Domain.Patients;
namespace Wasla.Tests.Unit;
public sealed class Phase5FinalIdentityApplicationTests
{
    [Theory] [InlineData(null, null, null, false)] [InlineData(null, "01000000000", null, true)] [InlineData("p@example.test", null, null, true)] [InlineData(null, null, "مريض", false)]
    public void Global_lookup_requires_bounded_predicate(string? email, string? phone, string? name, bool expected) => Assert.Equal(expected, PatientIdentityWorkflow.BoundedSearch(email, phone, name, null));
    [Fact] public async Task New_mutations_require_uuid_idempotency_keys()
    {
        var services = new ServiceCollection().AddWaslaApplication(); await using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IValidator<PatientIdentityCommand>>();
        var input = new PatientIdentityInput { Email = "synthetic@example.test", DoctorPracticeId = Guid.NewGuid() };
        Assert.False((await validator.ValidateAsync(new(PatientIdentityMutation.Full, input), TestContext.Current.CancellationToken)).IsValid);
        Assert.True((await validator.ValidateAsync(new(PatientIdentityMutation.Full, input, Guid.NewGuid().ToString()), TestContext.Current.CancellationToken)).IsValid);
    }
    [Fact] public void Default_retention_requires_operator_enablement_and_exact_approved_lifetimes()
    {
        var o = new PatientIdentityOptions(); Assert.True(o.IsValid()); Assert.False(o.EvidencePurgingEnabled); Assert.False(o.AuditPurgingEnabled);
        Assert.Equal(10, o.EmailOtpTtlMinutes); Assert.Equal(5, o.EmailOtpMaxAttempts); Assert.Equal(60, o.EmailOtpResendCooldownSeconds);
        Assert.Equal(30, o.VerifiedContinuationTtlMinutes); Assert.Equal(48, o.ActivationInvitationTtlHours); Assert.Equal(90, o.ClaimInactivityExpiryDays); Assert.Equal(24, o.AuditAfterClosureRetentionMonths);
    }
}
