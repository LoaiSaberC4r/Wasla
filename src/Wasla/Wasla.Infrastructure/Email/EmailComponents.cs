using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Wasla.Application.Email;

namespace Wasla.Infrastructure.Email;

internal sealed record EmailBlock(string Html, string Text)
{
    internal static EmailBlock Join(IEnumerable<EmailBlock> blocks)
    {
        var html = new StringBuilder();
        var text = new StringBuilder();
        foreach (var block in blocks)
        {
            html.Append(block.Html);
            if (text.Length > 0) text.Append("\n\n");
            text.Append(block.Text);
        }
        return new(html.ToString(), text.ToString());
    }
}

internal sealed class EmailComponents(EmailBrandingOptions options, string? assetBaseUrl)
{
    private const string PrimarySurface = "#F0F8F6";
    private const string PrimaryBorder = "#CCE7DF";
    private const string White = "#FFFFFF";
    private const string SecondarySurface = "#F8FAFC";
    private const string Border = "#E2E8F0";
    private const string InfoBackground = "#EFF6FF";
    private const string InfoText = "#1E40AF";
    private const string WarningBackground = "#FFFBEB";
    private const string WarningText = "#92400E";
    private const string Font = "Tahoma,Arial,sans-serif";

    private readonly string primary = Color(options.PrimaryColor, "#087F75");
    private readonly string primaryLight = Color(options.PrimaryLightColor, "#E8F5F2");
    private readonly string heading = Color(options.HeadingColor, "#172B36");
    private readonly string body = Color(options.BodyColor, "#334155");
    private readonly string muted = Color(options.MutedColor, "#64748B");
    private readonly string background = Color(options.BackgroundColor, "#EDF2F4");
    private readonly string success = Color(options.SuccessColor, "#166534");
    private readonly string warning = Color(options.WarningColor, "#B45309");
    private readonly string danger = Color(options.DangerColor, "#B91C1C");

    internal string Layout(EmailLanguageMode language, string subject, EmailBlock header, EmailBlock main, EmailBlock footer)
    {
        var english = language == EmailLanguageMode.English;
        return $"""
            <!doctype html>
            <html {Direction(english)}><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{Encode(subject)}</title></head>
            <body style="margin:0;padding:0;background:{background};font-family:{Font};color:{body};font-size:15px;line-height:1.7">
            <table role="presentation" dir="ltr" width="100%" cellspacing="0" cellpadding="0" bgcolor="{background}"><tr><td align="center" style="padding:16px 8px">
            <!--[if mso]><table role="presentation" width="640" align="center"><tr><td><![endif]-->
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" bgcolor="{White}" style="max-width:640px;border:1px solid {Border};border-radius:12px;border-spacing:0">
            {header.Html}
            <tr><td style="padding:24px;font-family:{Font};font-size:15px;line-height:1.7;color:{body}">{main.Html}</td></tr>
            {footer.Html}
            </table>
            <!--[if mso]></td></tr></table><![endif]-->
            </td></tr></table></body></html>
            """;
    }

    internal EmailBlock Header(EmailLanguageMode language, string? logo)
    {
        var name = new Localized<string>(
            Default(options.ApplicationNameAr, "وصلة"), Default(options.ApplicationNameEn, "Wasla"));
        var tagline = new Localized<string>(
            Default(options.TaglineAr, "رعاية صحية أكثر ترابطًا"), Default(options.TaglineEn, "More Connected Healthcare"));
        var branding = logo is null ? LocalizedInline(name, language) :
            $"<img src=\"{Encode(logo)}\" alt=\"{Encode(InlineText(name, language))}\" width=\"128\" style=\"display:block;width:128px;max-width:100%;height:auto;margin:0 auto;border:0\">";
        return new(
            $"""
            <tr><td id="wasla-header" align="center" style="padding:22px 16px 18px;border-bottom:4px solid {primary}">
            <div style="font-size:24px;font-weight:bold;color:{primary}">{branding}</div>
            <div style="font-size:13px;color:{muted}">{LocalizedInline(tagline, language, "<br>")}</div>
            </td></tr>
            """,
            InlineText(name, language) + "\n" + InlineText(tagline, language, "\n"));
    }

    internal EmailBlock Footer(EmailLanguageMode language, int year, string? image,
        string? website, string? support, string? privacy)
    {
        var disclaimer = new Localized<string>("هذه رسالة تلقائية، يرجى عدم الرد عليها.", "This is an automated email. Please do not reply.");
        var copyright = new Localized<string>(
            $"© {year} {Default(options.ApplicationNameAr, "وصلة")}. جميع الحقوق محفوظة.",
            $"© {year} {Default(options.ApplicationNameEn, "Wasla")}. All rights reserved.");
        var name = new Localized<string>(Default(options.ApplicationNameAr, "وصلة"), Default(options.ApplicationNameEn, "Wasla"));
        (string? Url, Localized<string> Label)[] links =
        [
            (website, new("الموقع الإلكتروني", "Website")),
            (support, new("الدعم", "Support")),
            (privacy, new("سياسة الخصوصية", "Privacy Policy"))
        ];
        var available = links.Where(link => link.Url is not null).ToArray();
        var footerLinks = available.Length == 0 ? "" : "<div style=\"margin:6px 0\">" +
            string.Join(" · ", available.Select(link =>
                $"<a href=\"{Encode(link.Url!)}\" style=\"color:{muted};text-decoration:underline\">{LocalizedInline(link.Label, language)}</a>")) + "</div>";
        var footerImage = image is null ? "" :
            $"<img src=\"{Encode(image)}\" alt=\"{Encode(InlineText(name, language))}\" width=\"592\" style=\"display:block;width:100%;max-width:592px;margin:10px auto 0;height:auto;border:0\">";
        var text = string.Join("\n", new[] { InlineText(name, language) }
            .Concat(available.Select(link => $"{InlineText(link.Label, language)}: {link.Url}"))
            .Concat([InlineText(disclaimer, language, "\n"), InlineText(copyright, language, "\n")]));
        return new(
            $"""
            <tr><td id="wasla-footer" align="center" style="padding:18px 16px;background:{SecondarySurface};border-top:3px solid {primary};color:{muted};font-size:13px;line-height:1.7">
            <strong>{LocalizedInline(name, language)}</strong>{footerLinks}
            <div>{LocalizedInline(disclaimer, language, "<br>")}</div>
            <div>{LocalizedInline(copyright, language, "<br>")}</div>{footerImage}
            </td></tr>
            """, text);
    }

    internal EmailBlock Hero(EmailTemplate template, EmailLanguageMode language)
    {
        var (color, fill) = Tone(template.Tone);
        var english = language == EmailLanguageMode.English;
        var subtitle = language == EmailLanguageMode.Bilingual
            ? $"<p {Direction(true)} style=\"font-size:15px;color:{muted};margin:4px 0 0\">{Encode(template.Heading.En)}</p>" : "";
        return new($"""
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin-bottom:20px">
            <tr><td align="center">
            <table role="presentation" width="72" cellspacing="0" cellpadding="0" align="center"><tr>
            <td width="72" height="72" align="center" valign="middle" aria-hidden="true" bgcolor="{fill}" style="border-radius:50%;font-size:30px;font-weight:bold;color:{color}">{Icon(template.Icon, template.Tone, 36)}</td>
            </tr></table>
            <h1 {Direction(english)} style="margin:12px 0 0;font-size:24px;line-height:1.35;color:{heading}">{Encode(template.Heading.For(english))}</h1>
            {subtitle}
            </td></tr></table>
            """, InlineText(template.Heading, language, "\n"));
    }

    internal static EmailBlock Paragraph(string value) => new(
        $"<p style=\"margin:0 0 12px;overflow-wrap:anywhere\">{Lines(value)}</p>", value);

    internal static EmailBlock LanguageSection(bool english, bool divider, IEnumerable<EmailBlock> blocks)
    {
        var rendered = EmailBlock.Join(blocks);
        var top = divider ? $"border-top:1px solid {Border};padding-top:16px;" : "";
        return new($"""
            <table role="presentation" {Direction(english)} width="100%" cellspacing="0" cellpadding="0" style="margin-bottom:8px">
            <tr><td align="{(english ? "left" : "right")}" style="{top}">{rendered.Html}</td></tr></table>
            """, rendered.Text);
    }

    internal EmailBlock Details(IEnumerable<(string Label, string? Value)> data, (string Label, string Value, string Tone)? status)
    {
        var html = new StringBuilder();
        var text = new List<string>();
        foreach (var (label, value) in data)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            AppendRow(label, value, Lines(value));
        }
        if (status is { } state)
            AppendRow(state.Label, state.Value, Status(state.Value, state.Tone));

        return new(Card(html.ToString(), SecondarySurface, Border), string.Join("\n", text));

        void AppendRow(string label, string value, string formattedValue)
        {
            var divider = text.Count == 0 ? "" : $"border-top:1px solid {Border};";
            html.Append(CultureInfo.InvariantCulture, $"""
                <tr><td width="42%" valign="top" style="{divider}padding:7px 4px;color:{muted};font-size:13px">{Encode(label)}</td>
                <td valign="top" style="{divider}padding:7px 4px;font-weight:bold;overflow-wrap:anywhere"><span dir="auto">{formattedValue}</span></td></tr>
                """);
            text.Add($"{label}: {value}");
        }
    }

    internal EmailBlock Action(Localized<string> label, EmailLanguageMode language, string destination) => new(
        $"""
        <table role="presentation" cellspacing="0" cellpadding="0" align="center" style="margin:4px auto 16px">
        <tr><td align="center" bgcolor="{primary}" style="border-radius:8px;mso-padding-alt:14px 22px">
        <a href="{Encode(destination)}" style="display:inline-block;padding:14px 22px;font-weight:bold;color:#fff;text-decoration:none">{ActionLabel(label, language)}</a>
        </td></tr></table>
        """, $"{InlineText(label, language)}: {destination}");

    internal EmailBlock Otp(string code, int minutes, EmailLanguageMode language)
    {
        var expiration = new Localized<string>($"صالح لمدة {minutes} دقائق فقط.", $"Valid for {minutes} minutes.");
        var label = new Localized<string>("رمز التحقق", "Verification code");
        var digits = string.Concat(code.Select(digit =>
            $"<td width=\"36\" height=\"42\" align=\"center\" bgcolor=\"{White}\" style=\"border:1px solid {PrimaryBorder};border-radius:8px;font-size:23px;font-weight:bold;color:{primary}\">{Encode(digit.ToString())}</td>"));
        var cells = $"""
            <tr><td align="center">
            <strong>{LocalizedInline(label, language)}</strong>
            <table role="presentation" dir="ltr" lang="en" cellspacing="4" cellpadding="0" align="center" style="margin:6px auto"><tr>{digits}</tr></table>
            <div style="color:{muted};font-size:13px">{LocalizedInline(expiration, language, "<br>")}</div>
            </td></tr>
            """;
        return new(Card(cells, PrimarySurface, PrimaryBorder), $"{InlineText(label, language)}: {code}\n{InlineText(expiration, language, "\n")}");
    }

    internal EmailBlock Activation(string token, string? destination, Localized<string> actionLabel,
        Localized<string> expiration, EmailLanguageMode language)
    {
        var expiryHtml = $"<div style=\"color:{muted};font-size:13px;text-align:center\">{LocalizedInline(expiration, language, "<br>")}</div>";
        var expiryText = InlineText(expiration, language, "\n");
        if (destination is not null)
            return EmailBlock.Join([Action(actionLabel, language, destination), new($"<div style=\"margin:0 0 16px\">{expiryHtml}</div>", expiryText)]);

        var label = new Localized<string>("رمز التفعيل", "Activation Token");
        return new(Card($"""
            <tr><td align="center">
            <strong>{LocalizedInline(label, language)}</strong>
            <div dir="ltr" style="margin:8px 0;font-family:Consolas,'Courier New',monospace;font-weight:bold;color:{primary};overflow-wrap:anywhere;word-break:break-all">{Encode(token)}</div>
            {expiryHtml}
            </td></tr>
            """, PrimarySurface, PrimaryBorder), $"{InlineText(label, language)}\n{token}\n{expiryText}");
    }

    internal static EmailBlock Security(string title, string message) => Callout("⚠", title, message, WarningBackground, WarningText);

    internal static EmailBlock Information(string title, string message) => Callout("ℹ", title, message, InfoBackground, InfoText);

    internal EmailBlock Stepper(bool english)
    {
        (string Stage, string State, string Marker, string Color, string Fill)[] steps = english
            ? [("Registration received", "Completed", "✓", primary, primaryLight),
               ("Data review", "Current", "2", warning, WarningBackground),
               ("Review decision", "Pending", "3", muted, Border)]
            : [("استلام الطلب", "مكتمل", "✓", primary, primaryLight),
               ("مراجعة البيانات", "المرحلة الحالية", "2", warning, WarningBackground),
               ("نتيجة المراجعة", "في انتظار القرار", "3", muted, Border)];
        var indicators = string.Concat(steps.Select((step, index) => $"""
            <td width="33%" align="center">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr>
            <td width="50%" valign="middle">{(index == 0 ? "" : Connector(index == 1 ? primary : "#94A3B8"))}</td>
            <td align="center" style="padding:0 4px">
            <table role="presentation" width="30" cellspacing="0" cellpadding="0"><tr>
            <td width="30" height="30" align="center" bgcolor="{step.Fill}" style="border:1px solid {step.Color};border-radius:50%;font-size:14px;font-weight:bold;color:{step.Color}">{step.Marker}</td>
            </tr></table>
            </td>
            <td width="50%" valign="middle">{(index == steps.Length - 1 ? "" : Connector(index == 0 ? primary : "#94A3B8"))}</td>
            </tr></table>
            </td>
            """));
        var labels = string.Concat(steps.Select(step => $"""
            <td align="center" valign="top" style="padding:8px 4px 0;font-size:13px;line-height:1.45;color:{heading};overflow-wrap:anywhere">
            <strong>{Encode(step.Stage)}</strong><br><span style="color:{step.Color}">{Encode(step.State)}</span>
            </td>
            """));
        var rows = $"<tr>{indicators}</tr><tr>{labels}</tr>";
        return new(Card(rows, PrimarySurface, PrimaryBorder), string.Join("\n", steps.Select(step => $"{step.Stage} — {step.State}")));

        static string Connector(string color) =>
            $"<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td height=\"2\" bgcolor=\"{color}\" style=\"font-size:1px;line-height:2px\">&nbsp;</td></tr></table>";
    }

    internal EmailBlock Steps(TemplateSteps steps, bool english)
    {
        var sectionTitle = steps.Heading.For(english);
        var html = new StringBuilder();
        var text = new List<string> { sectionTitle };
        html.Append(CultureInfo.InvariantCulture, $"<tr><td colspan=\"2\" style=\"font-size:18px;font-weight:bold;color:{heading}\">{Encode(sectionTitle)}</td></tr>");
        for (var index = 0; index < steps.Items.Length; index++)
        {
            var feature = steps.Items[index];
            var title = feature.Title.For(english);
            if (steps.Numbered) title = $"{(index + 1).ToString(CultureInfo.InvariantCulture)}. {title}";
            var description = feature.Description.For(english);
            var divider = index == 0 ? "" : $"border-top:1px solid {PrimaryBorder};";
            html.Append(CultureInfo.InvariantCulture, $"""
                <tr>
                <td width="42" valign="middle" style="{divider}padding:10px 0">
                <table role="presentation" width="42" cellspacing="0" cellpadding="0"><tr>
                <td width="42" height="42" align="center" valign="middle" aria-hidden="true" bgcolor="{White}" style="border-radius:9px;color:{primary};font-size:20px;font-weight:bold">{Icon(feature.Icon, "Neutral", 22)}</td>
                </tr></table>
                </td>
                <td valign="middle" style="{divider}padding:10px 9px;line-height:1.45">
                <strong style="color:{heading}">{Encode(title)}</strong><br><span style="font-size:13px;color:{muted}">{Encode(description)}</span>
                </td>
                </tr>
                """);
            text.Add($"{title} — {description}");
        }
        return new(Card(html.ToString(), PrimarySurface, PrimaryBorder), string.Join("\n", text));
    }

    private string Icon(EmailIcon icon, string tone, int size)
    {
        var fallback = icon switch
        {
            EmailIcon.LockKeyhole => "🔒",
            EmailIcon.MailCheck => "✉",
            EmailIcon.ClipboardX or EmailIcon.CalendarX => "×",
            EmailIcon.ShieldAlert => "!",
            EmailIcon.FilePen => "✎",
            EmailIcon.CalendarClock or EmailIcon.Clock or EmailIcon.Hourglass => "◷",
            EmailIcon.CalendarPlus or EmailIcon.UserRoundPlus => "+",
            EmailIcon.CalendarSync => "↻",
            EmailIcon.HeartPulse => "♥",
            EmailIcon.Stethoscope or EmailIcon.Hospital => "+",
            EmailIcon.Settings => "⚙",
            EmailIcon.Fingerprint or EmailIcon.Gift or EmailIcon.UsersRound or EmailIcon.UserRound or EmailIcon.BellRing => "•",
            _ => "✓"
        };
        if (assetBaseUrl is null) return Encode(fallback);

        var suffix = tone switch { "Success" => "-success", "Warning" => "-warning", "Danger" => "-danger", _ => "" };
        var file = JsonNamingPolicy.KebabCaseLower.ConvertName(icon.ToString()) + suffix + ".png";
        return $"<img src=\"{Encode(assetBaseUrl + "icons/" + file)}\" alt=\"{Encode(fallback)}\" width=\"{size}\" height=\"{size}\" style=\"display:block;border:0;margin:0 auto\">";
    }

    private static EmailBlock Callout(string icon, string title, string message, string fill, string color) => new(
        Card($"<tr><td><strong style=\"font-size:18px;color:{color}\"><span aria-hidden=\"true\">{icon}</span> {Encode(title)}</strong><div style=\"margin-top:5px;overflow-wrap:anywhere\">{Lines(message)}</div></td></tr>", fill, fill),
        $"{title}\n{message}");

    private string Status(string value, string tone)
    {
        var (color, fill) = Tone(tone);
        return $"<span style=\"display:inline-block;padding:1px 8px;border-radius:8px;color:{color};background:{fill};font-size:13px\">{Encode(value)}</span>";
    }

    private (string Color, string Background) Tone(string tone) => tone switch
    {
        "Success" => (success, "#DCFCE7"),
        "Warning" => (warning, WarningBackground),
        "Danger" => (danger, "#FFF1F2"),
        _ => (primary, primaryLight)
    };

    private static string Card(string rows, string fill, string border) => $"""
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:0 0 16px;border-radius:8px;background:{fill};border:1px solid {border}">
        <tr><td style="padding:16px"><table role="presentation" width="100%" cellspacing="0" cellpadding="0">{rows}</table></td></tr></table>
        """;

    private static string LocalizedInline(Localized<string> value, EmailLanguageMode language, string divider = " | ") => language switch
    {
        EmailLanguageMode.Arabic => Span(value.Ar, false),
        EmailLanguageMode.English => Span(value.En, true),
        _ => Span(value.Ar, false) + divider + Span(value.En, true)
    };

    private static string ActionLabel(Localized<string> label, EmailLanguageMode language) =>
        language == EmailLanguageMode.Bilingual
            ? Span(label.Ar, false) + $"<br><span style=\"font-size:12px;font-weight:normal\">{Span(label.En, true)}</span>"
            : LocalizedInline(label, language);

    internal static string InlineText(Localized<string> value, EmailLanguageMode language, string divider = " | ") => language switch
    {
        EmailLanguageMode.Arabic => value.Ar,
        EmailLanguageMode.English => value.En,
        _ => value.Ar + divider + value.En
    };

    private static string Span(string value, bool english) => $"<span {Direction(english)}>{Encode(value)}</span>";
    private static string Direction(bool english) => english ? "lang=\"en\" dir=\"ltr\"" : "lang=\"ar\" dir=\"rtl\"";
    private static string Lines(string value) => Encode(value).ReplaceLineEndings("<br>");
    private static string Default(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static string Color(string? value, string fallback) =>
        value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(char.IsAsciiHexDigit) ? value : fallback;

    internal static string Encode(string value) => WebUtility.HtmlEncode(value);
}
