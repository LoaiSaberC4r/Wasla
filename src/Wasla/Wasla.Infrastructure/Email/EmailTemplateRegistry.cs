using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wasla.Infrastructure.Email;

internal sealed class EmailTemplateRegistry
{
    // The design revision is independent of the stored notification event template version.
    private const string CatalogResource = "Wasla.Infrastructure.Email.EmailTemplates.v1.json";
    private static readonly JsonSerializerOptions CatalogOptions = new(JsonSerializerOptions.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter<EmailIcon>(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false),
            new JsonStringEnumConverter<EmailActionPlacement>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        }
    };

    private readonly FrozenDictionary<string, EmailTemplate> templates = Load();

    internal EmailTemplate Get(string key) => templates.TryGetValue(key, out var template)
        ? template
        : throw new ArgumentException("Unknown email template.");

    private static FrozenDictionary<string, EmailTemplate> Load()
    {
        using var stream = typeof(EmailTemplateRegistry).Assembly.GetManifestResourceStream(CatalogResource)
            ?? throw new InvalidOperationException("Email template catalog is missing.");
        var catalog = JsonSerializer.Deserialize<EmailTemplate[]>(stream, CatalogOptions)
            ?? throw new InvalidOperationException("Email template catalog is invalid.");
        return catalog.ToFrozenDictionary(template => template.Key, StringComparer.Ordinal);
    }
}

internal sealed record Localized<T>(T Ar, T En)
{
    internal T For(bool english) => english ? En : Ar;
}

internal enum EmailIcon
{
    LockKeyhole, MailCheck, UserRoundCheck, ClipboardCheck, ShieldCheck, ClipboardX, ShieldAlert, FilePen,
    Fingerprint, UsersRound, UserRoundPlus, CalendarCheck, CalendarClock, CalendarX, BellRing, CalendarSync, Gift,
    Clock, Hourglass, CalendarPlus, BadgeCheck, Stethoscope, HeartPulse, UserRound, Hospital, Settings
}

internal enum EmailActionPlacement
{
    AfterIntroduction, AfterDetails, AfterInstructions, BeforeClosing
}

internal sealed record EmailTemplate(string Key, Localized<string> Subject, Localized<string> Heading, EmailIcon Icon,
    string Tone, Localized<string[]>? Introduction, TemplateInfo? Information, Localized<string>? Security,
    TemplateSteps? Steps, TemplateAction? Action, TemplateActivation? Activation);
internal sealed record TemplateInfo(Localized<string> Heading, Localized<string> Body);
internal sealed record TemplateSteps(Localized<string> Heading, TemplateFeature[] Items, bool Numbered);
internal sealed record TemplateFeature(EmailIcon Icon, Localized<string> Title, Localized<string> Description);
internal sealed record TemplateAction(string Destination, Localized<string> Label, EmailActionPlacement Placement);
internal sealed record TemplateActivation(ActivationCopy SecureLink, ActivationCopy TokenFallback);
internal sealed record ActivationCopy(Localized<string> Introduction, string BilingualEnglishIntroduction,
    Localized<string> Expiration, Localized<string> Security);
