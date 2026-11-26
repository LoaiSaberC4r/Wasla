using System.Collections.Frozen;
using System.Text.Json;

namespace Wasla.Infrastructure.Email;

internal sealed class EmailTemplateRegistry
{
    // The design revision is independent of the stored notification event template version.
    private const string CatalogResource = "Wasla.Infrastructure.Email.EmailTemplates.v1.json";

    private readonly FrozenDictionary<string, EmailTemplate> templates = Load();

    internal EmailTemplate Get(string key) => templates.TryGetValue(key, out var template)
        ? template
        : throw new ArgumentException("Unknown email template.");

    private static FrozenDictionary<string, EmailTemplate> Load()
    {
        using var stream = typeof(EmailTemplateRegistry).Assembly.GetManifestResourceStream(CatalogResource)
            ?? throw new InvalidOperationException("Email template catalog is missing.");
        var catalog = JsonSerializer.Deserialize<EmailTemplate[]>(stream, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Email template catalog is invalid.");
        return catalog.ToFrozenDictionary(template => template.Key, StringComparer.Ordinal);
    }
}

internal sealed record Localized<T>(T Ar, T En)
{
    internal T For(bool english) => english ? En : Ar;
}

internal sealed record EmailTemplate(string Key, Localized<string> Subject, Localized<string> Heading, string Icon,
    string Tone, Localized<string[]> Introduction, TemplateInfo? Information, Localized<string>? Security,
    TemplateSteps? Steps, TemplateAction? Action);
internal sealed record TemplateInfo(Localized<string> Heading, Localized<string> Body);
internal sealed record TemplateSteps(Localized<string> Heading, Localized<string[]> Items);
internal sealed record TemplateAction(string Destination, Localized<string> Label);
