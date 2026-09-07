using System.Globalization;
using System.Xml.Linq;
using BuildingBlock.Application.Time;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application;
using Wasla.Application.Email;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;

namespace Wasla.Tests.Unit;

public sealed class TrustAccessDomainTests
{
    private static readonly DateTime Now = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ApplicationUser_NormalizesAndManagesCredentialState()
    {
        var user = ApplicationUser.Create(
            Guid.NewGuid(),
            "  Ahmed  ",
            "  AHMED@EXAMPLE.COM ",
            " 0100 ",
            "hash",
            UserType.Patient,
            isFirstLogin: true,
            Now).Value;

        Assert.Equal("Ahmed", user.UserName);
        Assert.Equal("ahmed@example.com", user.Email);
        Assert.Equal("0100", user.PhoneNumber);
        user.Deactivate();
        Assert.False(user.IsActive);
        user.Activate();
        user.ChangePassword("new-hash", Now.AddMinutes(1));
        Assert.True(user.IsActive);
        Assert.False(user.IsFirstLogin);
        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Fact]
    public void Gender_HasOnlyApprovedValues()
        => Assert.Equal([Gender.Male, Gender.Female], Enum.GetValues<Gender>());

    [Fact]
    public void Doctor_LifecycleEnforcesTransitionsAndNationalId()
    {
        var doctor = CreateDoctor(new DateOnly(1991, 4, 12));
        Assert.Equal(DoctorApprovalStatus.Pending, doctor.ApprovalStatus);
        Assert.Equal(35, doctor.GetAge(new DateOnly(2026, 4, 12)));
        Assert.True(doctor.Approve(" ", Guid.NewGuid(), Now).IsFailure);

        Assert.True(doctor.Approve(" 123 ", Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal("123", doctor.NationalId);
        Assert.Equal(DoctorApprovalStatus.Approved, doctor.ApprovalStatus);
        Assert.True(doctor.Reject("reason", Guid.NewGuid(), Now).IsFailure);
        Assert.True(doctor.Suspend(" reason ", Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(DoctorApprovalStatus.Suspended, doctor.ApprovalStatus);
        Assert.True(doctor.Reactivate(Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(DoctorApprovalStatus.Approved, doctor.ApprovalStatus);
    }

    [Fact]
    public void Doctor_PendingCanBeRejectedButNotSuspended()
    {
        var doctor = CreateDoctor(new DateOnly(2000, 9, 8));
        Assert.Equal(25, doctor.GetAge(new DateOnly(2026, 9, 7)));
        Assert.True(doctor.Suspend("reason", Guid.NewGuid(), Now).IsFailure);
        Assert.True(doctor.Reject(" reason ", Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(DoctorApprovalStatus.Rejected, doctor.ApprovalStatus);
        Assert.Equal("reason", doctor.RejectionReason);
        Assert.True(doctor.Approve("123", Guid.NewGuid(), Now).IsFailure);
    }

    [Fact]
    public void Patient_AgeIsDerivedFromDateOfBirth()
    {
        var patient = Patient.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "مريض",
            null,
            new DateOnly(2010, 9, 8),
            Gender.Female,
            null,
            null,
            null,
            new DateOnly(2026, 9, 7)).Value;

        Assert.Equal(15, patient.GetAge(new DateOnly(2026, 9, 7)));
        Assert.DoesNotContain(patient.GetType().GetProperties(), property => property.Name == "Age");
    }

    [Fact]
    public void RootSuperAdmin_IsImmutableThroughBusinessMethods()
    {
        var root = SuperAdmin.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "الجذر",
            "Root",
            isRootSuperAdmin: true,
            createdByApplicationUserId: null).Value;
        var normal = SuperAdmin.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "مشرف",
            null,
            isRootSuperAdmin: false,
            createdByApplicationUserId: Guid.NewGuid()).Value;

        Assert.True(root.EnsureMutable().IsFailure);
        Assert.True(root.UpdateNames("تغيير", null).IsFailure);
        Assert.True(normal.EnsureMutable().IsSuccess);
        Assert.False(normal.IsRootSuperAdmin);
        Assert.False(typeof(SuperAdmin).GetProperty(nameof(SuperAdmin.IsRootSuperAdmin))!.SetMethod!.IsPublic);
    }

    [Fact]
    public void PasswordResetChallenge_EnforcesAttemptsExpiryAndOneTimeUse()
    {
        var challenge = PasswordResetChallenge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash",
            Now,
            Now.AddMinutes(5)).Value;

        Assert.True(challenge.RegisterFailedAttempt(Now, 2).IsSuccess);
        Assert.True(challenge.RegisterFailedAttempt(Now, 2).IsSuccess);
        Assert.True(challenge.IsInvalidated);
        Assert.False(challenge.CanVerify(Now, 2));

        var verified = PasswordResetChallenge.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash",
            Now,
            Now.AddMinutes(5)).Value;
        Assert.True(verified.Verify(Now, 5).IsSuccess);
        Assert.True(verified.IssueResetToken("token-hash", Now.AddMinutes(10), Now).IsSuccess);
        Assert.True(verified.CanReset(Now));
        Assert.True(verified.Consume(Now).IsSuccess);
        Assert.True(verified.Consume(Now).IsFailure);
        Assert.True(verified.IsConsumed);
        Assert.True(challenge.IsExpired(Now.AddMinutes(6)));
    }

    [Fact]
    public void ErrorResources_HaveKeyParityAndResolveBothCultures()
    {
        var root = FindRepositoryRoot();
        var resources = Path.Combine(root, "src", "Wasla", "Wasla.Domain", "Resources");
        var english = ReadKeys(Path.Combine(resources, "ErrorMessage.resx"));
        var arabic = ReadKeys(Path.Combine(resources, "ErrorMessage.ar.resx"));
        Assert.Equal(english.Order(), arabic.Order());

        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var en = ErrorMessage.DoctorNotFound;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar");
            var ar = ErrorMessage.DoctorNotFound;
            Assert.NotEqual(en, ar);
            Assert.Contains("الطبيب", ar, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void EmailTemplates_AreBilingualEscapedAndUseConfiguredFooter()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(new FixedClock(Now));
        services.AddSingleton<IEmailBrandingProvider>(new FixedBranding("https://api.example.test/email-assets/wasla-email-footer.png?a=1&b=2"));
        services.AddWaslaApplication();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IEmailNotificationFactory>();

        var otp = factory.PasswordResetOtp("123456", 5);
        var rejected = factory.DoctorLifecycle(DoctorEmailEvent.Rejected, "<Ahmed>", "bad & unsafe");
        Assert.Contains("lang=\"ar\"", otp.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("lang=\"en\"", otp.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("123456", otp.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("wasla-email-footer.png?a=1&amp;b=2", otp.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<Ahmed>", rejected.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;Ahmed&gt;", rejected.HtmlBody, StringComparison.Ordinal);
    }

    private static Doctor CreateDoctor(DateOnly dateOfBirth)
        => Doctor.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "طبيب",
            "Doctor",
            dateOfBirth,
            Gender.Male,
            null,
            "Doctors/id/Verification/PersonalIdentity/Front/a.png",
            "Doctors/id/Verification/PersonalIdentity/Back/b.png",
            "Doctors/id/Verification/SyndicateCard/Front/c.png",
            null,
            new DateOnly(2026, 9, 7)).Value;

    private static HashSet<string> ReadKeys(string path)
        => XDocument.Load(path).Root!.Elements("data")
            .Select(element => element.Attribute("name")!.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Wasla.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException();
    }

    private sealed class FixedClock(DateTime value) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = value;
    }

    private sealed class FixedBranding(string footerImageUrl) : IEmailBrandingProvider
    {
        public string FooterImageUrl { get; } = footerImageUrl;
    }
}
