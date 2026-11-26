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
            Assert.Contains("src=\"" + SampleEmails.AssetBaseUrl + "icons/", result.HtmlBody, StringComparison.Ordinal);
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
        settings["EmailBranding:AssetBaseUrl"] = "https://unapproved.test/email-assets/";
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
        Assert.DoesNotContain("activation link", activation.TextBody!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Your activation token is valid for 48 hours.", activation.TextBody, StringComparison.Ordinal);
        Assert.Contains("Use the activation token above", activation.TextBody, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(activation.HtmlBody, "SYNTHETIC-TOKEN"));
        Assert.Single(Regex.Matches(activation.TextBody!, "SYNTHETIC-TOKEN"));
        InOrder(activation.HtmlBody, "SYNTHETIC-TOKEN", "استكشف خدمات وصلة", "wasla-footer");
        InOrder(activation.TextBody!, "SYNTHETIC-TOKEN", "استكشف خدمات وصلة", "الموقع الإلكتروني");
    }

    [Fact]
    public async Task Ctas_appear_once_at_the_configured_point_in_the_first_language()
    {
        using var services = SampleEmails.BuildServices(SampleEmails.Branding());
        var factory = services.GetRequiredService<IEmailNotificationFactory>();
        var ct = TestContext.Current.CancellationToken;
        var activation = new PatientAccountActivationEmail("SYNTHETIC-LINK-TOKEN", 48);
        var bilingual = await factory.CreateAsync(activation, ct);
        const string activationUrl = "https://app.wasla.test/activate?token=SYNTHETIC-LINK-TOKEN";
        Assert.Single(Regex.Matches(bilingual.HtmlBody, Regex.Escape(activationUrl)));
        Assert.Single(Regex.Matches(bilingual.TextBody!, Regex.Escape(activationUrl)));
        InOrder(bilingual.HtmlBody, activationUrl, "استكشف خدمات وصلة", "Use the activation button above");
        Assert.Contains("Your activation link is valid for 48 hours.", bilingual.TextBody, StringComparison.Ordinal);
        var english = await factory.CreateAsync(activation with { Language = EmailLanguageMode.English }, ct);
        InOrder(english.TextBody!, "Use the activation button below", activationUrl, "Explore Wasla");

        var approved = await factory.CreateAsync(new DoctorAccountApprovedEmail(new("ليلى", "Layla"),
            new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc)), ct);
        const string login = "https://app.wasla.test/sign-in";
        Assert.Single(Regex.Matches(approved.HtmlBody, Regex.Escape(login)));
        InOrder(approved.HtmlBody, "تاريخ الاعتماد", login, "الخطوات الأولى", "Hello Dr. Layla,");
        InOrder(approved.TextBody!, "تاريخ الاعتماد", login, "الخطوات الأولى", "Hello Dr. Layla,");
        Assert.Contains("26 نوفمبر 2026، 12:00 ظهرًا", approved.TextBody, StringComparison.Ordinal);
        Assert.Contains("26 Nov 2026, 12:00 PM", approved.TextBody, StringComparison.Ordinal);
        Assert.Contains("icons/shield-check-success.png", approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("icons/hospital.png", approved.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Booking_content_is_encoded_and_sensitive_templates_accept_only_permitted_fields()
    {
        using var services = SampleEmails.BuildServices();
        var factory = services.GetRequiredService<IEmailNotificationFactory>();
        var ct = TestContext.Current.CancellationToken;
        var booking = await factory.CreateAsync(new AppointmentReminderEmail("<script>\r\n", new(2026, 11, 30, 18, 30, 0)), ct);
        var family = await factory.CreateAsync(new FamilyRelationshipRejectedEmail(new("سلمى"), 3), ct);
        var clinical = await factory.CreateAsync(new MedicalUpdateAvailableEmail(), ct);

        Assert.Contains("&lt;script&gt;", booking.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", booking.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Appointment time: 06:30 PM", booking.TextBody, StringComparison.Ordinal);
        Assert.Contains("تاريخ الموعد: 30 نوفمبر 2026", booking.TextBody, StringComparison.Ordinal);
        Assert.Contains("وقت الموعد: 06:30 مساءً", booking.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Reason:", family.TextBody, StringComparison.Ordinal);
        Assert.Equal("Wasla | تحديث جديد على وصلة | New Wasla Update", clinical.Subject);
        Assert.DoesNotContain("Reservation reference", clinical.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Subscription_copy_uses_localized_plan_names_with_per_language_code_fallback()
    {
        using var services = SampleEmails.BuildServices();
        var factory = services.GetRequiredService<IEmailNotificationFactory>();
        var reminder = await factory.CreateAsync(new TrialReminderEmail(new("ليلى", "Layla"), new("DEMO", "باقة وصلة"),
            new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc)), TestContext.Current.CancellationToken);

        Assert.Contains("الباقة: باقة وصلة", reminder.TextBody, StringComparison.Ordinal);
        Assert.Contains("Plan: DEMO", reminder.TextBody, StringComparison.Ordinal);
        Assert.Contains("End date: 26 Nov 2026, 10:00 AM (UTC)", reminder.TextBody, StringComparison.Ordinal);
        Assert.Contains("Remaining time: 1 hour", reminder.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("hour(s)", reminder.TextBody, StringComparison.Ordinal);
    }

    private static void InOrder(string content, params string[] snippets)
    {
        var offset = 0;
        foreach (var snippet in snippets)
        {
            var position = content.IndexOf(snippet, offset, StringComparison.Ordinal);
            Assert.True(position >= 0, $"Expected {snippet} after character {offset}.");
            offset = position + snippet.Length;
        }
    }
}
