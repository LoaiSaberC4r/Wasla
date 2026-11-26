using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Email;
using Wasla.EmailPreview;

var folder = Path.GetFullPath(args.FirstOrDefault() ?? "email-previews");
var icons = Directory.CreateDirectory(Path.Combine(folder, "icons"));
foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "icons")))
    File.Copy(file, Path.Combine(icons.FullName, Path.GetFileName(file)), overwrite: true);

var settings = SampleEmails.Branding();
using var services = SampleEmails.BuildServices(settings);
var factory = services.GetRequiredService<IEmailNotificationFactory>();

foreach (var sample in SampleEmails.Catalog)
    await WritePreviewsAsync(factory, sample, folder, relativeIconPath: "icons/");

settings.Remove("EmailBranding:PatientActivationUrl");
using var fallbackServices = SampleEmails.BuildServices(settings);
var fallbackFactory = fallbackServices.GetRequiredService<IEmailNotificationFactory>();
var fallback = SampleEmails.Catalog.Single(sample => sample.Model is PatientAccountActivationEmail) with
{
    Slug = "activation-fallback/03-patient-account-activation",
    Title = "Patient Account Activation (token fallback)"
};
Directory.CreateDirectory(Path.Combine(folder, "activation-fallback"));
await WritePreviewsAsync(fallbackFactory, fallback, folder, relativeIconPath: "../icons/");

using var stream = typeof(SampleEmails).Assembly.GetManifestResourceStream("Wasla.EmailPreview.PreviewIndex.html")
    ?? throw new InvalidOperationException("The preview index resource is missing.");
using var reader = new StreamReader(stream);
var index = await reader.ReadToEndAsync();
var options = string.Join("\n", SampleEmails.Catalog.Append(fallback).Select(sample =>
    $"<option value=\"{sample.Slug}\">{WebUtility.HtmlEncode(sample.Title)}</option>"));
await File.WriteAllTextAsync(Path.Combine(folder, "index.html"), index.Replace("<!--template-options-->", options, StringComparison.Ordinal));
Console.WriteLine($"Generated {SampleEmails.Catalog.Count} templates and the activation fallback in three language modes with plain-text previews.");
Console.WriteLine(Path.Combine(folder, "index.html"));

static async Task WritePreviewsAsync(IEmailNotificationFactory factory, SampleEmail sample, string folder, string relativeIconPath)
{
    foreach (var (mode, suffix) in new[]
    {
        (EmailLanguageMode.Bilingual, ""),
        (EmailLanguageMode.Arabic, ".ar"),
        (EmailLanguageMode.English, ".en")
    })
    {
        var email = await factory.CreateAsync(sample.Model with { Language = mode });
        // Preview the same image markup offline using the PNGs shipped with the API.
        var html = email.HtmlBody.Replace("src=\"" + SampleEmails.AssetBaseUrl + "icons/",
            "src=\"" + relativeIconPath, StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(folder, sample.Slug + suffix + ".html"), html);
        await File.WriteAllTextAsync(Path.Combine(folder, sample.Slug + suffix + ".txt"), $"Subject: {email.Subject}\n\n{email.TextBody}");
    }
}
