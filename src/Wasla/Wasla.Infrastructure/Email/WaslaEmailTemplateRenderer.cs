using System.Globalization;
using BuildingBlock.Application.Email;
using BuildingBlock.Application.Time;
using Wasla.Application.Email;
using static Wasla.Application.Email.EmailTemplateKeys;

namespace Wasla.Infrastructure.Email;

internal sealed class WaslaEmailTemplateRenderer(
    EmailTemplateRegistry registry, EmailBrandingProvider branding, IDateTimeProvider clock) : IEmailTemplateRenderer
{
    private readonly EmailComponents components = new(branding.Options);

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
        var activation = template.Key == PatientAccountActivation;
        var token = activation ? Required<string>(data, "ActivationToken") : null;
        var expiration = template.Key is PasswordResetOtp or PatientEmailVerification or PatientAccountActivation
            ? Required<int>(data, "Expiration") : 0;
        var destination = token is null
            ? template.Action is { } link ? branding.Destination(link.Destination) : null
            : branding.Urls.ActivationLink(options.PatientActivationUrl, options.PatientActivationTokenQueryParameter, token);
        var action = destination is not null && template.Action is { } cta
            ? components.Action(cta.Label, language, destination) : null;

        EmailBlock? shared = template.Key is PasswordResetOtp or PatientEmailVerification
            ? components.Otp(Required<string>(data, "OtpCode"), expiration, language)
            : activation ? action : null;

        var sections = new List<EmailBlock> { components.Hero(template, language) };
        if (language != EmailLanguageMode.English)
            sections.Add(RenderLanguage(false, shared, divider: false));
        if (language != EmailLanguageMode.Arabic)
            sections.Add(RenderLanguage(true, language == EmailLanguageMode.English ? shared : null,
                divider: language == EmailLanguageMode.Bilingual));
        if (!activation && action is not null) sections.Add(action);
        var content = EmailBlock.Join(sections);
        var header = components.Header(language, branding.Urls.Validate(options.LogoUrl));
        var footer = components.Footer(language, now.Year, branding.Urls.Validate(options.FooterImageUrl, legacyFooter: true),
            branding.Urls.Validate(options.WebsiteUrl), branding.Urls.Validate(options.SupportUrl), branding.Urls.Validate(options.PrivacyPolicyUrl));
        var subject = "Wasla | " + EmailComponents.InlineText(template.Subject, language);
        var text = EmailBlock.Join([header, content, footer]).Text;
        if (token is not null && action is null)
        {
            var tokenBlock = EmailComponents.ActivationToken(token, language);
            content = EmailBlock.Join([content, tokenBlock]);
            text += "\n\n" + tokenBlock.Text;
        }
        return Task.FromResult(new EmailTemplateResult(subject, components.Layout(language, subject, header, content, footer), text));

        EmailBlock RenderLanguage(bool english, EmailBlock? sharedComponent, bool divider)
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

            if (english && sharedComponent is not null) blocks.Add(sharedComponent);
            if (activation && action is null)
            {
                blocks.Add(EmailComponents.Paragraph(L(
                    "مرحبًا بك في وصلة. استخدم رمز التفعيل الوارد في هذه الرسالة لإنشاء كلمة مرور وتفعيل حسابك.",
                    "Welcome to Wasla. Use the activation token in this email to create your password and activate your account.")));
            }
            else
            {
                blocks.AddRange(template.Introduction.For(english).Select(EmailComponents.Paragraph));
            }
            if (!english && sharedComponent is not null) blocks.Add(sharedComponent);

            if (activation)
                blocks.Add(EmailComponents.Paragraph(L($"دعوة التفعيل صالحة لمدة {expiration} ساعة.",
                    $"Your activation invitation is valid for {expiration} hours.")));

            var details = Details(template.Key, data, now, english);
            if (details is not null) blocks.Add(details);

            if (template.Key == DoctorRegistrationReceived) blocks.Add(components.Stepper(english));
            if (template.Steps is { } steps)
                blocks.Add(components.Steps(steps.Heading.For(english), steps.Items.For(english), numbered: !activation));
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
            if (template.Security is { } security)
                blocks.Add(EmailComponents.Security(L("تنبيه أمني", "Security alert"), security.For(english)));
            blocks.Add(EmailComponents.Paragraph(L("مع تحيات فريق وصلة", "Best regards, Wasla Team")));
            return EmailComponents.LanguageSection(english, divider, blocks);
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
            var date = UtcDate(when);
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
                var date = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
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
        if (Get<string>(data, "Plan") is { } plan)
        {
            Row("الباقة", "Plan", plan);
            if (Get<DateTime?>(data, "StartsAtUtc") is { } start)
                Row("تاريخ البداية", "Start date", UtcDate(start));
            if (Get<DateTime?>(data, "EndsAtUtc") is { } end)
            {
                if (key == TrialExtended) Row("تاريخ الانتهاء الجديد", "New end date", UtcDate(end));
                else Row("تاريخ الانتهاء", "End date", UtcDate(end));

                if (key is TrialReminder or PaidSubscriptionReminder)
                {
                    var time = end - now;
                    var remaining = time.TotalDays >= 1
                        ? L($"{Number((int)time.TotalDays)} يوم", $"{Number((int)time.TotalDays)} day(s)")
                        : time.TotalHours >= 1
                            ? L($"{Number((int)time.TotalHours)} ساعة", $"{Number((int)time.TotalHours)} hour(s)")
                            : time > TimeSpan.Zero ? L("أقل من ساعة", "Less than an hour") : L("انتهت المدة", "Period ended");
                    Row("الوقت المتبقي", "Remaining time", remaining);
                }
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
    private static string UtcDate(DateTime value) => value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static T? Get<T>(IReadOnlyDictionary<string, object?> data, string key) =>
        data.TryGetValue(key, out var value) && value is T typed ? typed : default;

    private static T Required<T>(IReadOnlyDictionary<string, object?> data, string key) =>
        data.TryGetValue(key, out var value) && value is T typed ? typed : throw new ArgumentException("Missing email rendering field.");
}
