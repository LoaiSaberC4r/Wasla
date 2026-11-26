using System.Globalization;
using BuildingBlock.Application.Email;
using BuildingBlock.Application.Time;
using Wasla.Application.Email;
using static Wasla.Application.Email.EmailTemplateKeys;

namespace Wasla.Infrastructure.Email;

internal sealed class WaslaEmailTemplateRenderer(
    EmailTemplateRegistry registry, EmailBrandingProvider branding, IDateTimeProvider clock) : IEmailTemplateRenderer
{
    private readonly EmailComponents components = new(branding.Options, branding.Urls.AssetBaseUrl(branding.Options.AssetBaseUrl));
    private readonly EmailDateFormatter dates = new(branding.Options.TimeZoneId);

    public Task<EmailTemplateResult> RenderAsync(EmailTemplateRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var template = registry.Get(request.TemplateName);
        var language = request.Culture switch
        {
            "ar" => EmailLanguageMode.Arabic,
            "en" => EmailLanguageMode.English,
            "ar-en" => EmailLanguageMode.Bilingual,
            _ => throw new ArgumentException("Unsupported email language.")
        };
        var options = branding.Options;
        var data = request.Model;
        var now = clock.UtcNow;
        var token = template.Activation is not null ? Required<string>(data, "ActivationToken") : null;
        var expiration = template.Key is PasswordResetOtp or PatientEmailVerification or PatientAccountActivation
            ? Required<int>(data, "Expiration") : 0;
        var destination = token is null
            ? template.Action is { } link ? branding.Destination(link.Destination) : null
            : branding.Urls.ActivationLink(options.PatientActivationUrl, options.PatientActivationTokenQueryParameter, token);
        var activationCopy = destination is null ? template.Activation?.TokenFallback : template.Activation?.SecureLink;
        var action = template.Action is { } cta
            ? CreateAction(cta) : null;
        var otp = template.Key is PasswordResetOtp or PatientEmailVerification
            ? components.Otp(Required<string>(data, "OtpCode"), expiration, language) : null;

        var sections = new List<EmailBlock> { components.Hero(template, language) };
        if (language != EmailLanguageMode.English)
            sections.Add(RenderLanguage(english: false, first: true));
        if (language != EmailLanguageMode.Arabic)
            sections.Add(RenderLanguage(english: true, first: language == EmailLanguageMode.English));
        var content = EmailBlock.Join(sections);
        var header = components.Header(language, branding.Urls.Validate(options.LogoUrl));
        var footer = components.Footer(language, now.Year, branding.Urls.Validate(options.FooterImageUrl, legacyFooter: true),
            branding.Urls.Validate(options.WebsiteUrl), branding.Urls.Validate(options.SupportUrl), branding.Urls.Validate(options.PrivacyPolicyUrl));
        var subject = "Wasla | " + EmailComponents.InlineText(template.Subject, language);
        var text = EmailBlock.Join([header, content, footer]).Text;
        return Task.FromResult(new EmailTemplateResult(subject, components.Layout(language, subject, header, content, footer), text));

        EmailBlock? CreateAction(TemplateAction cta)
        {
            if (activationCopy is not null && token is not null)
            {
                var validity = new Localized<string>(
                    string.Format(CultureInfo.InvariantCulture, activationCopy.Expiration.Ar, expiration),
                    string.Format(CultureInfo.InvariantCulture, activationCopy.Expiration.En, expiration));
                return components.Activation(token, destination, cta.Label, validity, language);
            }
            return destination is null ? null : components.Action(cta.Label, language, destination);
        }

        EmailBlock RenderLanguage(bool english, bool first)
        {
            string L(string ar, string en) => english ? en : ar;
            var blocks = new List<EmailBlock>();
            if (Get<EmailName>(data, "Name") is { } name)
            {
                var display = Name(name, english);
                var doctor = template.Key.StartsWith("Doctor.", StringComparison.Ordinal) ||
                    template.Key.StartsWith("Subscription.", StringComparison.Ordinal);
                blocks.Add(EmailComponents.Paragraph(doctor
                    ? L($"مرحبًا د. {display}،", $"Hello Dr. {display},")
                    : L($"مرحبًا {display}،", $"Hello {display},")));
            }

            if (first && english && otp is not null) blocks.Add(otp);
            if (activationCopy is not null)
                blocks.Add(EmailComponents.Paragraph(english && !first
                    ? activationCopy.BilingualEnglishIntroduction : activationCopy.Introduction.For(english)));
            else if (template.Introduction is { } introduction)
                blocks.AddRange(introduction.For(english).Select(EmailComponents.Paragraph));
            if (!english && otp is not null) blocks.Add(otp);
            AddAction(EmailActionPlacement.AfterIntroduction);

            var details = Details(template.Key, data, now, english);
            if (details is not null) blocks.Add(details);
            AddAction(EmailActionPlacement.AfterDetails);

            if (template.Key == DoctorRegistrationReceived) blocks.Add(components.Stepper(english));
            if (template.Steps is { } steps) blocks.Add(components.Steps(steps, english));
            if (Get<string>(data, "ReviewMessage") is { } message &&
                template.Key is DoctorRegistrationRejected or DoctorAccountSuspended or DoctorSpecializationChangesRequested)
            {
                var title = template.Key switch
                {
                    DoctorRegistrationRejected => L("سبب عدم الاعتماد", "Reason for rejection"),
                    DoctorAccountSuspended => L("سبب تعليق الحساب", "Reason for suspension"),
                    _ => L("ملاحظات المراجعة", "Review message")
                };
                blocks.Add(EmailComponents.Information(title, message));
            }
            if (template.Information is { } info)
                blocks.Add(EmailComponents.Information(info.Heading.For(english), info.Body.For(english)));
            if ((activationCopy?.Security ?? template.Security) is { } security)
                blocks.Add(EmailComponents.Security(L("تنبيه أمني", "Security alert"), security.For(english)));
            AddAction(EmailActionPlacement.AfterInstructions);
            AddAction(EmailActionPlacement.BeforeClosing);
            blocks.Add(EmailComponents.Paragraph(L("مع تحيات فريق وصلة", "Best regards, Wasla Team")));
            return EmailComponents.LanguageSection(english, divider: !first, blocks);

            void AddAction(EmailActionPlacement placement)
            {
                if (first && action is not null && template.Action?.Placement == placement) blocks.Add(action);
            }
        }
    }

    private EmailBlock? Details(string key, IReadOnlyDictionary<string, object?> data, DateTime now, bool english)
    {
        string L(string ar, string en) => english ? en : ar;
        var rows = new List<(string Label, string? Value)>();
        void Row(string ar, string en, string? value) => rows.Add((L(ar, en), value));
        var status = key switch
        {
            DoctorRegistrationReceived => State("قيد المراجعة", "Pending Review", "Warning"),
            DoctorAccountApproved or DoctorAccountReactivated or FamilyRelationshipApproved => State("معتمد", "Approved", "Success"),
            DoctorRegistrationRejected or FamilyRelationshipRejected => State("غير معتمد", "Rejected", "Danger"),
            DoctorAccountSuspended => State("معلّق", "Suspended", "Warning"),
            DoctorSpecializationChangesRequested or FamilyRelationshipChangesRequested => State("مطلوب تعديل", "Changes Requested", "Warning"),
            ReservationConfirmed => State("مؤكد", "Confirmed", "Success"),
            ReservationCancelled => State("ملغى", "Cancelled", "Danger"),
            TrialExpired or SubscriptionExpired => State("منتهي", "Expired", "Danger"),
            PaidSubscriptionActivated => State("نشط", "Active", "Success"),
            _ => ((string Label, string Value, string Tone)?)null
        };
        (string, string, string) State(string ar, string en, string tone) => (L("الحالة", "Status"), L(ar, en), tone);

        if (Get<DateTime?>(data, "OccurredAtUtc") is { } when && Get<EmailName>(data, "Name") is { } doctor)
        {
            Row("اسم الطبيب", "Doctor name", Name(doctor, english));
            var date = dates.PlatformDateTime(when, english);
            switch (key)
            {
                case DoctorRegistrationReceived: Row("تاريخ التسجيل", "Registration date", date); break;
                case DoctorAccountApproved:
                    Row("تاريخ الاعتماد", "Approval date", date);
                    Row("نوع الحساب", "Account type", L("طبيب", "Doctor"));
                    break;
                case DoctorRegistrationRejected: Row("تاريخ المراجعة", "Review date", date); break;
                case DoctorAccountSuspended: Row("تاريخ التعليق", "Suspension date", date); break;
                case DoctorAccountReactivated: Row("تاريخ إعادة التفعيل", "Reactivation date", date); break;
            }
        }
        if (Get<int?>(data, "Revision") is { } revision)
            Row("رقم الإصدار", "Revision", Number(revision));
        if (key == ReceptionAccountCreated)
        {
            Row("اسم موظف الاستقبال", "Reception name", Name(Required<EmailName>(data, "Name"), english));
            Row("اسم المستخدم", "Username", Required<string>(data, "Username"));
            Row("نوع الحساب", "Account type", L("استقبال", "Reception"));
        }
        if (Get<string>(data, "Reference") is { } reference)
        {
            Row("رقم الحجز", "Reservation reference", reference);
            if (Get<DateTime?>(data, "ScheduledLocalDateTime") is { Ticks: > 0 } local)
            {
                var date = EmailDateFormatter.LocalDate(local, english);
                var time = EmailDateFormatter.LocalTime(local, english);
                if (key == ReservationRescheduled)
                {
                    Row("تاريخ الموعد الجديد", "New appointment date", date);
                    Row("وقت الموعد الجديد", "New appointment time", time);
                }
                else
                {
                    Row("تاريخ الموعد", "Appointment date", date);
                    Row("وقت الموعد", "Appointment time", time);
                }
            }
        }
        if (Get<SubscriptionPlanEmailName>(data, "Plan") is { } plan)
        {
            var planName = english ? plan.NameEn : plan.NameAr;
            Row("الباقة", "Plan", string.IsNullOrWhiteSpace(planName) ? plan.PlanCode : planName);
            if (Get<DateTime?>(data, "StartsAtUtc") is { } start)
                Row("تاريخ البداية", "Start date", dates.PlatformDateTime(start, english));
            if (Get<DateTime?>(data, "EndsAtUtc") is { } end)
            {
                if (key == TrialExtended) Row("تاريخ الانتهاء الجديد", "New end date", dates.PlatformDateTime(end, english));
                else Row("تاريخ الانتهاء", "End date", dates.PlatformDateTime(end, english));

                if (key is TrialReminder or PaidSubscriptionReminder)
                    Row("الوقت المتبقي", "Remaining time", EmailDateFormatter.Remaining(end - now, english));
            }
            if (Get<int?>(data, "PracticeLimit") is { } practices)
                Row("الحد الأقصى للعيادات", "Maximum practices", Number(practices));
            if (Get<int?>(data, "ReceptionLimit") is { } seats)
                Row("مقاعد الاستقبال", "Reception seats", Number(seats));
            if (Get<EmailName[]>(data, "LinkedPractices") is { Length: > 0 } names)
                Row("العيادات المرتبطة", "Linked practices", string.Join(L("، ", ", "), names.Select(name => Name(name, english))));
            if (Get<decimal?>(data, "RenewalPrice") is { } price)
            {
                Row("سعر التجديد المتاح", "Available renewal price", price.ToString("0.00", CultureInfo.InvariantCulture));
                Row("العملة", "Currency", Required<string>(data, "Currency"));
            }
        }

        return rows.Count > 0 || status is not null ? components.Details(rows, status) : null;
    }

    private static string Name(EmailName name, bool english) =>
        english && !string.IsNullOrWhiteSpace(name.English) ? name.English : name.Arabic;
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static T? Get<T>(IReadOnlyDictionary<string, object?> data, string key) =>
        data.TryGetValue(key, out var value) && value is T typed ? typed : default;

    private static T Required<T>(IReadOnlyDictionary<string, object?> data, string key) =>
        data.TryGetValue(key, out var value) && value is T typed ? typed : throw new ArgumentException("Missing email rendering field.");
}
