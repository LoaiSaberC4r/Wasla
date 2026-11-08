using System.Text;
using Wasla.Domain.Patients;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Media;
namespace Wasla.Tests.Unit;
public sealed class HistoricalDocumentSafetyTests
{
    [Theory]
    [InlineData("/JavaScript")]
    [InlineData("/J#61vaScript")]
    [InlineData("/OpenAction")]
    [InlineData("/EmbeddedFiles")]
    [InlineData("/ObjStm")]
    [InlineData("/Encrypt")]
    [InlineData("/URI")]
    public async Task Active_or_opaque_pdf_features_are_rejected(string feature)
    {
        await using var file = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\n" + feature + " 1 0 R\n%%EOF"));
        Assert.False(await HistoricalDocumentSafety.IsPassiveAsync(file, ".pdf", TestContext.Current.CancellationToken));
        Assert.Equal(0, file.Position);
    }
    [Fact]
    public async Task Policy_scans_name_tokens_across_buffer_boundaries()
    {
        await using var file = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\n" + new string(' ', 8179) + "/JavaScript 1 0 R\n%%EOF"));
        Assert.False(await HistoricalDocumentSafety.IsPassiveAsync(file, ".pdf", TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Truncated_pdf_and_image_bombs_are_rejected()
    {
        await using var file = new MemoryStream("%PDF-1.4"u8.ToArray());
        Assert.False(await HistoricalDocumentSafety.IsPassiveAsync(file, ".pdf", TestContext.Current.CancellationToken));
        var png = new byte[24]; new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0); "IHDR"u8.CopyTo(png.AsSpan(12));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16), 100000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20), 100000);
        await using var image = new MemoryStream(png);
        Assert.False(await HistoricalDocumentSafety.IsPassiveAsync(image, ".png", TestContext.Current.CancellationToken));
    }
    [Fact]
    public void Source_lifecycle_preserves_subject_and_never_represents_official_verification()
    {
        var subject = Guid.NewGuid(); var actor = Guid.NewGuid(); var parent = Guid.NewGuid(); var now = DateTime.UtcNow;
        var document = new PatientHistoricalDocument(subject, actor, parent, now);
        document.PublishValidatedVersion(1, now); document.PublishValidatedVersion(2, now.AddMinutes(1)); document.Withdraw(now.AddMinutes(2));
        Assert.Equal(subject, document.SubjectPatientId); Assert.Equal(actor, document.UploadedByApplicationUserId); Assert.Equal(parent, document.UploaderPatientId);
        Assert.Equal(2, document.CurrentVersionNumber); Assert.Equal(HistoricalDocumentState.Withdrawn, document.State);
        Assert.Throws<InvalidOperationException>(() => document.PublishValidatedVersion(3, now.AddMinutes(3)));
    }
}
