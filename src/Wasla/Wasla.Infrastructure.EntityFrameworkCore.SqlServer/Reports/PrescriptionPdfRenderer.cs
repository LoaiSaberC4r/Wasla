using System.Net;
using System.Text;
using BuildingBlock.Domain.Results;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Wasla.Application.Features.DoctorPatients;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Medications;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

public sealed class PrescriptionPdfRenderer(IOptions<Phase18Options> options) : IPrescriptionPdfRenderer, IDisposable
{
    private readonly SemaphoreSlim _renders = new(options.Value.MaximumConcurrentPdfRenders, options.Value.MaximumConcurrentPdfRenders);
    public static string Html(PrescriptionPdfDocument d)
    {
        static string E(object? value) => WebUtility.HtmlEncode(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "");
        var mark = d.Version.Status switch
        {
            PrescriptionVersionStatus.Superseded => "SUPERSEDED — NOT CURRENT / غير سارية",
            PrescriptionVersionStatus.Voided => "VOIDED — NOT VALID / ملغاة",
            _ => "FINALIZED / صادرة"
        };
        var html = new StringBuilder("<!doctype html><html lang='ar' dir='rtl'><head><meta charset='utf-8'><meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; style-src 'unsafe-inline'; script-src 'none'; connect-src 'none'; base-uri 'none'\"><style>body{font:14px Tahoma,'Segoe UI',Arial,sans-serif;color:#143c50;line-height:1.8}header{border-bottom:3px solid #14838c}h1{font-size:24px}.status{border:2px solid #983434;padding:10px;font-weight:bold;text-align:center}article{break-inside:avoid;border-bottom:1px solid #ccd;padding:12px 0}p{margin:4px 0;white-space:pre-wrap;overflow-wrap:anywhere}table{width:100%;table-layout:fixed;border-collapse:collapse}th,td{padding:3px 5px;text-align:start;vertical-align:top;overflow-wrap:anywhere}th{width:35%;font-weight:normal;color:#456271}bdi{unicode-bidi:isolate}.meta{font-size:12px}footer{margin-top:20px;font-size:11px}</style></head><body><header><h1>WASLA · وصفة طبية / Prescription</h1>");
        html.Append(System.Globalization.CultureInfo.InvariantCulture, $"<p>{E(d.PracticeName)}</p><p>الطبيب / Doctor: {E(d.DoctorName)}</p></header><p>المريض / Patient: {E(d.PatientName)}</p>");
        html.Append(System.Globalization.CultureInfo.InvariantCulture, $"<p class='meta' dir='ltr'>Encounter: {d.EncounterDateUtc:yyyy-MM-dd HH:mm} UTC · Version {d.Version.VersionNumber} · Issued: {d.Version.FinalizedAtUtc:yyyy-MM-dd HH:mm} UTC</p><p class='status' dir='auto'>{E(mark)}</p>");
        if (d.Version.Status != PrescriptionVersionStatus.Finalized)
            html.Append("<div class='watermark' dir='ltr' style='position:fixed;top:45%;left:0;width:100%;text-align:center;transform:rotate(-30deg);font:bold 36px Arial;color:#983434;opacity:0.10;z-index:10'>NOT CURRENT / NOT VALID</div><p class='status'>NOT VALID FOR CURRENT TREATMENT / غير صالحة للعلاج الحالي</p>");
        foreach (var item in d.Version.Items.OrderBy(i => i.SortOrder))
        {
            var c = item.Clinical;
            html.Append(System.Globalization.CultureInfo.InvariantCulture, $"<article><strong><bdi>{E(item.MedicationName)}</bdi></strong><p><bdi>{E(item.ScientificName)}</bdi></p><table>");
            void Field(string label, string? value) => html.Append(System.Globalization.CultureInfo.InvariantCulture, $"<tr><th>{E(label)}</th><td><bdi>{E(value)}</bdi></td></tr>");
            Field("التركيز / Strength", c.Strength); Field("الشكل / Form", c.DosageForm); Field("الطريق / Route", c.Route);
            Field("الجرعة / Dose", c.Dose); Field("التكرار / Frequency", Frequency(c));
            Field("المدة / Duration", c.DurationType == MedicationDurationType.Ongoing ? "Ongoing / مستمر" : FormattableString.Invariant($"{c.DurationValue} {c.DurationUnit}"));
            if (c.AsNeeded) { Field("عند اللزوم / PRN", c.PrnReason); Field("الفاصل الأدنى / Minimum interval", c.MinimumIntervalText); Field("الحد الأقصى / 24h", c.MaxPer24HoursText); }
            if (c.QuantityValue.HasValue) Field("الكمية / Quantity", FormattableString.Invariant($"{c.QuantityValue} {c.QuantityUnit}"));
            html.Append("</table>");
            html.Append(System.Globalization.CultureInfo.InvariantCulture, $"<p><bdi>{E(c.Instructions)}</bdi></p></article>");
        }
        html.Append(System.Globalization.CultureInfo.InvariantCulture, $"<footer><p dir='ltr'>Prescription {d.PrescriptionId:N} · Version {d.Version.VersionNumber}</p><p>Historical issued version. No certified electronic signature / نسخة صادرة محفوظة دون توقيع إلكتروني معتمد.</p></footer></body></html>");
        return html.ToString();
    }
    public async Task<Result<byte[]>> RenderAsync(PrescriptionPdfDocument document, CancellationToken ct)
    {
        if (document.Version.Status is not (PrescriptionVersionStatus.Finalized or PrescriptionVersionStatus.Superseded or PrescriptionVersionStatus.Voided) || document.Version.FinalizedAtUtc is null)
            return Result<byte[]>.Fail(MedicationErrors.NotFound("Prescription.NotFound"));
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ChromiumExecutablePath) || !Path.IsPathFullyQualified(o.ChromiumExecutablePath) || !File.Exists(o.ChromiumExecutablePath))
            return Result<byte[]>.Fail(DoctorPatientErrors.PdfUnavailable("PrescriptionPdf.ChromiumNotConfigured"));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(o.PdfTimeoutSeconds));
        var entered = false;
        try
        {
            await _renders.WaitAsync(timeout.Token); entered = true;
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { ExecutablePath = o.ChromiumExecutablePath,
                Headless = true, Timeout = o.PdfTimeoutSeconds * 1000,
                Args = ["--disable-background-networking", "--disable-extensions", "--disable-sync", "--no-first-run"] });
            await using var page = await browser.NewPageAsync();
            await page.SetJavaScriptEnabledAsync(false); await page.SetOfflineModeAsync(true);
            await page.SetRequestInterceptionAsync(true);
            page.Request += async (_, e) =>
            {
                try { await e.Request.AbortAsync(); }
                catch (PuppeteerException) { /* Page already closed. */ }
            };
            await page.SetContentAsync(Html(document), new SetContentOptions { WaitUntil = [WaitUntilNavigation.Load], Timeout = o.PdfTimeoutSeconds * 1000 }).WaitAsync(timeout.Token);
            var status = document.Version.Status == PrescriptionVersionStatus.Finalized ? "FINALIZED" : document.Version.Status == PrescriptionVersionStatus.Voided ? "VOIDED - NOT VALID" : "SUPERSEDED - NOT CURRENT";
            var bytes = await page.PdfDataAsync(new PdfOptions { Format = PaperFormat.A4, PrintBackground = true, DisplayHeaderFooter = true,
                HeaderTemplate = "<span></span>",
                FooterTemplate = FormattableString.Invariant($"<div style='width:100%;text-align:center;direction:ltr;font:8px Tahoma,Arial;color:#64748b'>WASLA · Version {document.Version.VersionNumber} · {status} · <span class='pageNumber'></span> / <span class='totalPages'></span></div>"),
                MarginOptions = new MarginOptions { Top = "12mm", Bottom = "12mm", Left = "12mm", Right = "12mm" } }).WaitAsync(timeout.Token);
            if (bytes.Length > new ExportLimits().PdfMaximumBytes) return Result<byte[]>.Fail(DoctorPatientErrors.PdfUnavailable("PrescriptionPdf.DocumentTooLarge"));
            return bytes.AsSpan().StartsWith("%PDF"u8) ? Result<byte[]>.Ok(bytes) : Result<byte[]>.Fail(DoctorPatientErrors.PdfUnavailable("PrescriptionPdf.InvalidPdf"));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Result<byte[]>.Fail(DoctorPatientErrors.PdfUnavailable("PrescriptionPdf.RenderTimeout")); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Result<byte[]>.Fail(DoctorPatientErrors.PdfUnavailable("PrescriptionPdf.RenderUnavailable")); }
        finally { if (entered) _renders.Release(); }
    }
    private static string Frequency(PrescriptionItemData item)
    {
        var label = item.FrequencyCode switch
        {
            MedicationFrequency.OnceDaily => "Once daily / مرة يومياً", MedicationFrequency.TwiceDaily => "Twice daily / مرتين يومياً",
            MedicationFrequency.ThreeTimesDaily => "Three times daily / ثلاث مرات يومياً", MedicationFrequency.FourTimesDaily => "Four times daily / أربع مرات يومياً",
            MedicationFrequency.Every4Hours => "Every 4 hours / كل ٤ ساعات", MedicationFrequency.Every6Hours => "Every 6 hours / كل ٦ ساعات",
            MedicationFrequency.Every8Hours => "Every 8 hours / كل ٨ ساعات", MedicationFrequency.Every12Hours => "Every 12 hours / كل ١٢ ساعة",
            MedicationFrequency.Morning => "Morning / صباحاً", MedicationFrequency.Bedtime => "Bedtime / عند النوم",
            MedicationFrequency.OnceOnly => "Once only / مرة واحدة", _ => ""
        };
        return string.IsNullOrWhiteSpace(item.FrequencyText) ? label : label.Length == 0 ? item.FrequencyText : label + " · " + item.FrequencyText;
    }
    public void Dispose() => _renders.Dispose();
}
