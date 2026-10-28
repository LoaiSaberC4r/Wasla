using Microsoft.Extensions.Options;
using Wasla.Application.Features.DoctorPatients;
using Wasla.Application.Features.Reporting;
using Wasla.Domain.Medications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

namespace Wasla.Tests.Unit;

public sealed class DoctorPatientPdfTests
{
    [Theory]
    [InlineData(PrescriptionVersionStatus.Finalized, "FINALIZED")]
    [InlineData(PrescriptionVersionStatus.Superseded, "SUPERSEDED — NOT CURRENT")]
    [InlineData(PrescriptionVersionStatus.Voided, "VOIDED — NOT VALID")]
    public async Task Issued_versions_render_private_encoded_bilingual_documents(PrescriptionVersionStatus status, string label)
    {
        var doc = Document(status); var html = PrescriptionPdfRenderer.Html(doc);
        Assert.Contains(label, html); Assert.Contains("dir='rtl'", html);
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>", html);
        Assert.Contains("default-src 'none'", html); Assert.Contains("Version 2", html);
        Assert.Contains("الجرعة", html); Assert.DoesNotContain("private-reason", html);
        if (status != PrescriptionVersionStatus.Finalized) { Assert.Contains("NOT VALID FOR CURRENT TREATMENT", html); Assert.Contains("class='watermark'", html); }
        var executable = Environment.GetEnvironmentVariable("WASLA_CHROMIUM_PATH");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(executable), "WASLA_CHROMIUM_PATH is required for actual PDF rendering.");
        using var renderer = new PrescriptionPdfRenderer(Options.Create(new Phase18Options { ChromiumExecutablePath = executable }));
        var result = await renderer.RenderAsync(doc, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.IsFailure ? string.Join(",", result.Errors.Select(e => e.Code)) : "");
        Assert.True(result.Value.AsSpan().StartsWith("%PDF"u8)); Assert.True(result.Value.Length > 10000);
        var output = Environment.GetEnvironmentVariable("WASLA_EXPORT_QA_DIRECTORY");
        if (output is not null) { Directory.CreateDirectory(output); await File.WriteAllBytesAsync(Path.Combine(output, "prescription-" + status + ".pdf"), result.Value, TestContext.Current.CancellationToken); }
    }
    [Fact]
    public async Task Drafts_and_missing_runtime_never_produce_pdf_bytes()
    {
        using var renderer = new PrescriptionPdfRenderer(Options.Create(new Phase18Options()));
        Assert.True((await renderer.RenderAsync(Document(PrescriptionVersionStatus.Draft), TestContext.Current.CancellationToken)).IsFailure);
        Assert.True((await renderer.RenderAsync(Document((PrescriptionVersionStatus)999), TestContext.Current.CancellationToken)).IsFailure);
        Assert.True((await renderer.RenderAsync(Document(PrescriptionVersionStatus.Finalized), TestContext.Current.CancellationToken)).IsFailure);
    }
    private static PrescriptionPdfDocument Document(PrescriptionVersionStatus status)
    {
        var date = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);
        return new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "د. أحمد", "عيادة وصلة", "مريض الاختبار <script>", date,
            new(Guid.NewGuid(), 2, status, null, "private-reason", date, status == PrescriptionVersionStatus.Draft ? null : date,
                status == PrescriptionVersionStatus.Voided ? date : null, "private-reason",
                [new(Guid.NewGuid(), 1, MedicationSource.DoctorSubmitted, null, Guid.NewGuid(), "دواء تجريبي / Test medicine", "Synthetic",
                    new(Strength: "500 mg", DosageForm: "Tablet", Route: "Oral", Dose: "قرص واحد", FrequencyCode: MedicationFrequency.TwiceDaily,
                        DurationType: MedicationDurationType.Fixed, DurationValue: 5, DurationUnit: MedicationDurationUnit.Days,
                        Instructions: "بعد الأكل صباحاً ومساءً — للمراجعة فقط"))]));
    }
}
