using Wasla.Application.Features.DoctorPatients;
using Wasla.Domain.Medications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

namespace Wasla.Tests.Unit;

public sealed class DoctorPatientPdfTests
{
    [Theory]
    [InlineData(PrescriptionVersionStatus.Finalized, "FINALIZED")]
    [InlineData(PrescriptionVersionStatus.Superseded, "SUPERSEDED — NOT CURRENT")]
    [InlineData(PrescriptionVersionStatus.Voided, "VOIDED — NOT VALID")]
    public void Issued_versions_render_private_encoded_bilingual_documents(PrescriptionVersionStatus status, string label)
    {
        var doc = Document(status); var html = PrescriptionPdfRenderer.Html(doc);
        Assert.Contains(label, html); Assert.Contains("dir='rtl'", html);
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>", html);
        Assert.Contains("default-src 'none'", html); Assert.Contains("Version 2", html);
        Assert.Contains("الجرعة", html); Assert.DoesNotContain("private-reason", html);
        if (status != PrescriptionVersionStatus.Finalized) { Assert.Contains("NOT VALID FOR CURRENT TREATMENT", html); Assert.Contains("class='watermark'", html); }
    }
    [Fact]
    public async Task Drafts_never_reach_browser_and_launch_failure_is_controlled_without_path_configuration()
    {
        var renderer = new PrescriptionPdfRenderer(new PdfTestSupport.UnavailablePdf());
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal("Prescription.NotFound", (await renderer.RenderAsync(Document(PrescriptionVersionStatus.Draft), ct)).Errors.First().Code);
        Assert.True((await renderer.RenderAsync(Document((PrescriptionVersionStatus)999), ct)).IsFailure);
        Assert.Equal("PrescriptionPdf.BrowserLaunchFailed", (await renderer.RenderAsync(Document(PrescriptionVersionStatus.Finalized), ct)).Errors.First().Code);
    }
    internal static PrescriptionPdfDocument Document(PrescriptionVersionStatus status)
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
