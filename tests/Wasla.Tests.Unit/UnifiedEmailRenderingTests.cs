using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Email;
using Wasla.EmailPreview;

namespace Wasla.Tests.Unit;

public sealed class UnifiedEmailRenderingTests
{
    public static IEnumerable<object[]> Templates() =>
        SampleEmails.Catalog.Select((sample, index) => new object[] { index, sample.Slug });

    [Theory]
    [MemberData(nameof(Templates))]
    public async Task Each_template_renders_both_representations_in_three_modes(int index, string slug)
    {
        Assert.NotEmpty(slug);
        using var services = SampleEmails.BuildServices(SampleEmails.Branding());
        var factory = services.GetRequiredService<IEmailNotificationFactory>();
        var sample = SampleEmails.Catalog[index];

        foreach (var mode in Enum.GetValues<EmailLanguageMode>())
        {
            var result = await factory.CreateAsync(sample.Model with { Language = mode }, TestContext.Current.CancellationToken);
            Assert.StartsWith("Wasla | ", result.Subject, StringComparison.Ordinal);
            Assert.NotEmpty(result.HtmlBody);
            Assert.False(string.IsNullOrWhiteSpace(result.TextBody));
            Assert.Single(Regex.Matches(result.HtmlBody, "id=\"wasla-header\""));
            Assert.Single(Regex.Matches(result.HtmlBody, "id=\"wasla-footer\""));
            Assert.Contains(sample.Example, result.TextBody, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"\{[A-Za-z][A-Za-z0-9]*\}", result.HtmlBody);
            Assert.DoesNotMatch(@"\{[A-Za-z][A-Za-z0-9]*\}", result.TextBody);
            Assert.Equal(mode != EmailLanguageMode.English, result.HtmlBody.Contains("lang=\"ar\" dir=\"rtl\"", StringComparison.Ordinal));
            Assert.Equal(mode != EmailLanguageMode.Arabic, result.HtmlBody.Contains("lang=\"en\" dir=\"ltr\"", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Otp_keeps_leading_zero_and_respects_configured_length()
    {
        using var services = SampleEmails.BuildServices();
        var factory = services.GetRequiredService<IEmailNotificationFactory>();
        var email = await factory.CreateAsync(new PatientEmailVerificationEmail("0057194", 13), TestContext.Current.CancellationToken);
        var digits = Regex.Matches(email.HtmlBody, @"<td width=""36""[^>]*>([0-9])</td>");
        Assert.Equal("0057194", string.Concat(digits.Select(digit => digit.Groups[1].Value)));
        Assert.Single(Regex.Matches(email.TextBody!, "0057194"));
        Assert.Contains("Valid for 13 minutes.", email.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unsafe_links_are_omitted_and_activation_falls_back_to_the_token()
    {
        var settings = SampleEmails.Branding();
        settings["EmailBranding:LogoUrl"] = "https://app.wasla.test/logo.png";
        settings["EmailBranding:LoginUrl"] = "https://unapproved.test/login";
        settings["EmailBranding:SupportUrl"] = "javascript:alert(1)";
        settings["EmailBranding:PatientActivationUrl"] = "https://app.wasla.test@unapproved.test/activate";
        using var services = SampleEmails.BuildServices(settings);
        var factory = services.GetRequiredService<IEmailNotificationFactory>();
        var ct = TestContext.Current.CancellationToken;
        var clinical = await factory.CreateAsync(new MedicalUpdateAvailableEmail(), ct);
        var activation = await factory.CreateAsync(new PatientAccountActivationEmail("SYNTHETIC-TOKEN", 48), ct);

        Assert.Contains("https://app.wasla.test/logo.png", clinical.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("unapproved.test", clinical.HtmlBody + activation.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", clinical.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Activate My Account", activation.HtmlBody, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(activation.HtmlBody, "SYNTHETIC-TOKEN"));
        Assert.EndsWith("SYNTHETIC-TOKEN", activation.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Booking_content_is_encoded_and_sensitive_templates_accept_only_permitted_fields()
    {
        using var services = SampleEmails.BuildServices();
        var factory = services.GetRequiredService<IEmailNotificationFactory>();
        var ct = TestContext.Current.CancellationToken;
        var booking = await factory.CreateAsync(new AppointmentReminderEmail("<script>\r\n", new(2026, 11, 30, 14, 35, 0))
            { Language = EmailLanguageMode.English }, ct);
        var family = await factory.CreateAsync(new FamilyRelationshipRejectedEmail(new("سلمى"), 3), ct);
        var clinical = await factory.CreateAsync(new MedicalUpdateAvailableEmail(), ct);

        Assert.Contains("&lt;script&gt;", booking.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", booking.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Appointment time: 14:35", booking.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Reason:", family.TextBody, StringComparison.Ordinal);
        Assert.Equal("Wasla | تحديث جديد على وصلة | New Wasla Update", clinical.Subject);
        Assert.DoesNotContain("Reservation reference", clinical.TextBody, StringComparison.Ordinal);
    }
}
