# Unified Wasla transactional email

Business workflows call `IEmailNotificationFactory.CreateAsync` with a typed model. The Application factory validates the required inputs and maps the template's permitted fields to the existing BuildingBlock `EmailTemplateRequest`. Infrastructure selects the localized content from the embedded, versioned catalog and uses `WaslaEmailTemplateRenderer` to compose a subject, HTML body, and plain-text body.

Callers queue those three values through `IEmailOutbox` using their existing recipient, idempotency key, transaction, and delivery ID where applicable. The existing processor uses the stored content for every SMTP attempt. The factory and renderer depend on no database, network, SMTP, or save operation.

- [Template catalog and fields](EMAIL-TEMPLATE-CATALOG.md)
- [Design and localization](EMAIL-DESIGN-SYSTEM.md)
- [Branding and Frontend configuration](CONFIGURATION.md)
- [Migration and delivery report](IMPLEMENTATION-SUMMARY.md)

## Add a template

1. Add a typed model and key under `Wasla.Application/Email`. Limit the model to values the recipient is authorized to see.
2. Map the model's permitted fields in `EmailNotificationFactory`. Use the default bilingual language for direct system email or explicitly set the guarded recipient's preferred language.
3. Add the localized copy and registered icon to `EmailTemplates.v1.json`. Define any action in the catalog with its placement and approved destination. Use the shared components for dynamic content.
4. Add a synthetic preview sample. Connect the template to an approved existing trigger and reuse caller-owned Outbox queueing.

## Generate previews

Run the generator from the repository root.

```powershell
dotnet run --project tools/Wasla.EmailPreview
```

Open `email-previews/index.html` to choose a template, language, viewport, and format. Each template has a bilingual HTML and text file plus Arabic (`.ar`) and English (`.en`) variants. The index also includes Patient Account Activation (token fallback) in all three modes. An optional argument after `--` selects an alternate output directory. The CLI uses the production factory and renderer with synthetic data. It copies the published icon files into the preview folder so they load offline.
