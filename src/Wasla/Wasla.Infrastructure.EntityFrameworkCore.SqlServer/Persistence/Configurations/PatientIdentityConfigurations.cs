using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Patients;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal static class PatientIdentityConfiguration
{
    internal static void Common<T>(EntityTypeBuilder<T> b, string table) where T : class
    {
        b.ToTable(table); b.HasKey("Id"); b.Property<Guid>("Id").ValueGeneratedNever();
        foreach (var p in typeof(T).GetProperties().Where(p => p.PropertyType == typeof(string)))
        {
            var length = p.Name.Contains("Email", StringComparison.Ordinal) ? 200 :
                p.Name.Contains("Hash", StringComparison.Ordinal) || p.Name.Contains("Fingerprint", StringComparison.Ordinal) ? 256 :
                p.Name.Contains("Phone", StringComparison.Ordinal) ? 30 :
                p.Name is "ApplicantNameAr" or "ContentType" ? 200 : p.Name.EndsWith("IdempotencyKey", StringComparison.Ordinal) ? 100 :
                p.Name is "Reason" && typeof(T) == typeof(PatientIdentityAttestation) ? 500 : 1000;
            b.Property(p.Name).HasMaxLength(length);
        }
        if (typeof(T).GetProperty("RowVersion") is not null) b.Property<byte[]>("RowVersion").IsRowVersion().IsConcurrencyToken();
    }
}
internal sealed class PatientOnboardingChallengeConfiguration : IWriteEntityConfiguration<PatientOnboardingChallenge>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientOnboardingChallenge> b)
    {
        PatientIdentityConfiguration.Common(b, "PatientOnboardingChallenges");
        b.Property(x => x.MutationResponseJson).HasMaxLength(4000);
        b.HasIndex(x => x.ContinuationTokenHash).IsUnique().HasFilter("[ContinuationTokenHash] IS NOT NULL");
        b.HasIndex(x => new { x.NormalizedEmail, x.Purpose, x.CreatedOnUtc }).IsDescending(false, false, true);
        b.HasIndex(x => x.OtpExpiresOnUtc);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class PatientClaimRequestConfiguration : IWriteEntityConfiguration<PatientClaimRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientClaimRequest> b)
    {
        PatientIdentityConfiguration.Common(b, "PatientClaimRequests"); b.Ignore(x => x.IsTerminal);
        b.HasIndex(x => x.TargetPatientId).IsUnique().HasDatabaseName("UX_PatientClaims_OpenTarget")
            .HasFilter("[TargetPatientId] IS NOT NULL AND [Status] IN (1, 2, 3)");
        b.HasIndex(x => x.ClaimantNormalizedEmail).IsUnique().HasDatabaseName("UX_PatientClaims_OpenEmail")
            .HasFilter("[TargetPatientId] IS NULL AND [Status] IN (1, 2, 3)");
        b.HasIndex(x => new { x.ClaimantNormalizedEmail, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.Status, x.SubmittedOnUtc }); b.HasIndex(x => new { x.Status, x.LastActivityOnUtc });
        b.HasIndex(x => new { x.IsUnderLegalHold, x.ClosedOnUtc });
        b.HasOne<Patient>().WithMany().HasForeignKey(x => x.TargetPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(x => x.ApprovedTargetPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PatientOnboardingChallenge>().WithMany().HasForeignKey(x => x.SourceChallengeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.LinkedApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.LegalHoldSetByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class PatientClaimEvidenceConfiguration : IWriteEntityConfiguration<PatientClaimEvidence>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientClaimEvidence> b)
    {
        PatientIdentityConfiguration.Common(b, "PatientClaimEvidences");
        b.HasIndex(x => new { x.ClaimRequestId, x.RevisionNumber }); b.HasIndex(x => new { x.RetiredOnUtc, x.PurgedOnUtc });
        b.HasOne<PatientClaimRequest>().WithMany().HasForeignKey(x => x.ClaimRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PatientIdentityAttestation>().WithMany().HasForeignKey(x => x.IdentityAttestationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UploadedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class PatientClaimHistoryConfiguration : IWriteEntityConfiguration<PatientClaimHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientClaimHistory> b)
    {
        PatientIdentityConfiguration.Common(b, "PatientClaimHistories"); b.Property(x => x.SafeMetadataJson).HasMaxLength(4000);
        b.HasIndex(x => new { x.ClaimRequestId, x.OccurredOnUtc, x.Id });
        b.HasOne<PatientClaimRequest>().WithMany().HasForeignKey(x => x.ClaimRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class PatientActivationInvitationConfiguration : IWriteEntityConfiguration<PatientActivationInvitation>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientActivationInvitation> b)
    {
        PatientIdentityConfiguration.Common(b, "PatientActivationInvitations");
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.ResendIdempotencyKey).IsUnique().HasFilter("[ResendIdempotencyKey] IS NOT NULL");
        b.HasIndex(x => x.ClaimRequestId).IsUnique().HasFilter("[ClaimRequestId] IS NOT NULL AND [ConsumedOnUtc] IS NULL AND [RevokedOnUtc] IS NULL");
        b.HasIndex(x => x.ApplicationUserId).IsUnique().HasFilter("[ApplicationUserId] IS NOT NULL AND [ConsumedOnUtc] IS NULL AND [RevokedOnUtc] IS NULL");
        b.HasIndex(x => new { x.NormalizedEmail, x.CreatedOnUtc });
        b.HasOne<PatientClaimRequest>().WithMany().HasForeignKey(x => x.ClaimRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class PatientDuplicateCaseConfiguration : IWriteEntityConfiguration<PatientDuplicateCase>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientDuplicateCase> b)
    {
        PatientIdentityConfiguration.Common(b, "PatientDuplicateCases");
        b.ToTable("PatientDuplicateCases", t => t.HasCheckConstraint("CK_PatientDuplicateCases_Distinct", "[FirstPatientId] <> [SecondPatientId]"));
        b.HasIndex(x => new { x.LowerPatientId, x.HigherPatientId }).IsUnique().HasFilter("[Status] = 1"); b.HasIndex(x => x.Status);
        b.HasOne<Patient>().WithMany().HasForeignKey(x => x.FirstPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(x => x.SecondPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ResolvedBySuperAdminApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class PatientIdentityAttestationConfiguration : IWriteEntityConfiguration<PatientIdentityAttestation>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientIdentityAttestation> b)
    {
        PatientIdentityConfiguration.Common(b, "PatientIdentityAttestations");
        b.HasIndex(x => new { x.DoctorPracticeId, x.PatientId, x.ReceptionApplicationUserId, x.ExpiresOnUtc });
        b.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => x.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReceptionApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
