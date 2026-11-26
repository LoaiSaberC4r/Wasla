using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Wasla.Application.Email;
using Wasla.EmailPreview;

var folder = Path.GetFullPath(args.FirstOrDefault() ?? "email-previews");
Directory.CreateDirectory(folder);
using var services = SampleEmails.BuildServices(SampleEmails.Branding());
var factory = services.GetRequiredService<IEmailNotificationFactory>();

foreach (var sample in SampleEmails.Catalog)
{
    foreach (var (mode, suffix) in new[]
    {
        (EmailLanguageMode.Bilingual, ""),
        (EmailLanguageMode.Arabic, ".ar"),
        (EmailLanguageMode.English, ".en")
    })
    {
        var email = await factory.CreateAsync(sample.Model with { Language = mode });
        await File.WriteAllTextAsync(Path.Combine(folder, sample.Slug + suffix + ".html"), email.HtmlBody);
        await File.WriteAllTextAsync(Path.Combine(folder, sample.Slug + suffix + ".txt"), $"Subject: {email.Subject}\n\n{email.TextBody}");
    }
}

using var stream = typeof(SampleEmails).Assembly.GetManifestResourceStream("Wasla.EmailPreview.PreviewIndex.html")
    ?? throw new InvalidOperationException("The preview index resource is missing.");
using var reader = new StreamReader(stream);
var index = await reader.ReadToEndAsync();
var options = string.Join("\n", SampleEmails.Catalog.Select(sample =>
    $"<option value=\"{sample.Slug}\">{WebUtility.HtmlEncode(sample.Title)}</option>"));
await File.WriteAllTextAsync(Path.Combine(folder, "index.html"), index.Replace("<!--template-options-->", options, StringComparison.Ordinal));
Console.WriteLine($"Generated {SampleEmails.Catalog.Count} templates in three language modes with plain-text previews.");
Console.WriteLine(Path.Combine(folder, "index.html"));
