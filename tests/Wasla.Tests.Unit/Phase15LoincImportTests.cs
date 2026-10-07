using System.IO.Compression;
using System.Text;
using Wasla.Application.Features.Diagnostics;
using Wasla.Domain.Diagnostics;

namespace Wasla.Tests.Unit;

public sealed class Phase15LoincImportTests
{
    public static byte[] Package(string? extraPath = null, bool invalidHeaders = false, string status = "ACTIVE",
        string arabic = "اسم رسمي", bool extraFirst = false)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void File(string path, string text)
            {
                var entry = zip.CreateEntry(path); using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)); writer.Write(text);
            }
            if (extraPath is not null && extraFirst) File(extraPath, "unrelated noncanonical content");
            File("LoincTable/Loinc.csv", invalidHeaders ? "WRONG\nvalue" :
                "LOINC_NUM,COMPONENT,STATUS,CLASSTYPE,ORDER_OBS,LONG_COMMON_NAME,SHORTNAME,EXTERNAL_COPYRIGHT_NOTICE\n" +
                $"1234-5,Component,{status},1,Order,Common test,Common,Source copyright\n" +
                "2345-6,Other,ACTIVE,1,Both,Other test,Other,\n" +
                "3456-7,Observation,ACTIVE,1,Observation,Observation only,Observation,\n" +
                "4567-8,Clinical,ACTIVE,2,Order,Clinical test,Clinical,\n" +
                "5678-9,Imaging,ACTIVE,2,Order,Chest imaging,Chest,\n");
            File("AccessoryFiles/UniversalLabOrders/LoincUniversalLabOrdersValueSet.csv", "LOINC_NUM,LONG_COMMON_NAME\n1234-5,Common test\n");
            File("LinguisticVariants/arJO32LinguisticVariant.csv", $"LOINC_NUM,LONG_COMMON_NAME\n1234-5,{arabic}\n");
            File("AccessoryFiles/Radiology/LoincRsnaRadiologyPlaybook.csv", "LoincNumber,PartTypeName,PartName,PartNumber,PartSequenceOrder\n" +
                "5678-9,Rad.Modality.Modality type,XR,LP1,A\n5678-9,Rad.Anatomic Location.Region Imaged,Chest,LP2,A\n5678-9,Rad.Anatomic Location.Laterality,Left,LP3,A\n");
            if (extraPath is not null && !extraFirst) File(extraPath, "unrelated noncanonical content");
        }
        return stream.ToArray();
    }
    [Theory]
    [InlineData(DiagnosticKind.Lab, false)]
    [InlineData(DiagnosticKind.Lab, true)]
    [InlineData(DiagnosticKind.Radiology, false)]
    [InlineData(DiagnosticKind.Radiology, true)]
    public void Exact_canonical_table_ignores_unrelated_Loinc_basename_in_either_zip_order(DiagnosticKind kind, bool accessoryFirst)
    {
        var parsed = LoincPackageParser.Parse(Package("AccessoryFiles/PanelsAndForms/Loinc.csv", extraFirst: accessoryFirst), kind, "2.83", new(), TestContext.Current.CancellationToken);
        Assert.True(parsed.IsSuccess);
        Assert.Equal(kind == DiagnosticKind.Lab ? 2 : 1, parsed.Value.Count);
        Assert.Equal(kind == DiagnosticKind.Lab ? "Common test" : "Chest imaging", parsed.Value[0].Data.NameEn);
    }
    [Theory]
    [InlineData("loINCTaBLE/lOInc.CSV")]
    [InlineData("LoincTable\\Loinc.csv")]
    public void Exact_canonical_path_normalizes_slashes_and_case(string path)
    {
        var bytes = RewritePackage(name => name == "LoincTable/Loinc.csv" ? path : name);
        Assert.True(LoincPackageParser.Parse(bytes, DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken).IsSuccess);
    }
    [Theory]
    [InlineData("LoincTable/Loinc.csv")]
    [InlineData("loinctable/LOINC.CSV")]
    [InlineData("LoincTable\\Loinc.csv")]
    public void Conflicting_normalized_canonical_paths_are_rejected(string path)
    {
        var result = LoincPackageParser.Parse(Package(path), DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken);
        Assert.Equal("DiagnosticCatalogImport.UnsafeArchive", Assert.Single(result.Errors).Code);
    }
    [Theory]
    [InlineData("Loinc.csv")]
    [InlineData("wrapper/LoincTable/Loinc.csv")]
    public void Canonical_table_must_have_the_exact_official_relative_path(string path)
    {
        var result = LoincPackageParser.Parse(RewritePackage(name => name == "LoincTable/Loinc.csv" ? path : name), DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken);
        Assert.Equal("DiagnosticCatalogImport.RequiredFileMissing", Assert.Single(result.Errors).Code);
    }
    [Theory]
    [InlineData(DiagnosticKind.Lab, "Other/LoincUniversalLabOrdersValueSet.csv")]
    [InlineData(DiagnosticKind.Lab, "Other/arJO32LinguisticVariant.csv")]
    [InlineData(DiagnosticKind.Radiology, "Other/LoincRsnaRadiologyPlaybook.csv")]
    public void Ambiguous_accessory_basenames_are_rejected(DiagnosticKind kind, string path)
    {
        var result = LoincPackageParser.Parse(Package(path), kind, "2.83", new(), TestContext.Current.CancellationToken);
        Assert.Equal("DiagnosticCatalogImport.UnsafeArchive", Assert.Single(result.Errors).Code);
    }
    [Theory]
    [InlineData(DiagnosticKind.Lab, "LoincTable/Loinc.csv")]
    [InlineData(DiagnosticKind.Lab, "AccessoryFiles/UniversalLabOrders/LoincUniversalLabOrdersValueSet.csv")]
    [InlineData(DiagnosticKind.Radiology, "AccessoryFiles/Radiology/LoincRsnaRadiologyPlaybook.csv")]
    public void Missing_required_source_or_accessory_is_rejected(DiagnosticKind kind, string missingPath)
    {
        var result = LoincPackageParser.Parse(RewritePackage(name => name == missingPath ? null : name), kind, "2.83", new(), TestContext.Current.CancellationToken);
        Assert.Equal("DiagnosticCatalogImport.RequiredFileMissing", Assert.Single(result.Errors).Code);
    }
    [Fact]
    public void Missing_optional_arabic_retains_null_official_translation()
    {
        var parsed = LoincPackageParser.Parse(RewritePackage(name => name.EndsWith("arJO32LinguisticVariant.csv", StringComparison.Ordinal) ? null : name), DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken);
        Assert.True(parsed.IsSuccess);
        Assert.All(parsed.Value, row => Assert.Null(row.Data.OfficialNameAr));
    }
    [Fact]
    public void Seekable_stream_import_matches_byte_input_and_keeps_caller_stream_open()
    {
        var bytes = Package();
        using var stream = new MemoryStream(bytes, writable: false);
        stream.Position = stream.Length;
        var parsed = LoincPackageParser.Parse(stream, DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken);
        Assert.True(parsed.IsSuccess);
        Assert.Equal(LoincPackageParser.Parse(bytes, DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken).Value.Select(r => r.Hash), parsed.Value.Select(r => r.Hash));
        Assert.True(stream.CanRead);
        Assert.Equal("DiagnosticCatalogImport.PackageTooLarge", Assert.Single(LoincPackageParser.Parse(stream, DiagnosticKind.Lab, "2.83", new() { MaxPackageBytes = 10 }, TestContext.Current.CancellationToken).Errors).Code);
    }
    [Fact]
    public void Nonseekable_stream_is_rejected_without_implicit_zip_buffering()
    {
        using var stream = new NonseekableStream(Package());
        Assert.Equal("DiagnosticCatalogImport.InvalidArchive", Assert.Single(LoincPackageParser.Parse(stream, DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken).Errors).Code);
        Assert.True(stream.CanRead);
    }
    [Fact]
    public void Cancellation_before_and_during_stream_read_is_propagated()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => LoincPackageParser.Parse(Package(), DiagnosticKind.Lab, "2.83", new(), cancelled.Token));
        using var duringRead = new CancellationTokenSource();
        using var stream = new CancellingStream(Package(), duringRead);
        Assert.Throws<OperationCanceledException>(() => LoincPackageParser.Parse(stream, DiagnosticKind.Lab, "2.83", new(), duringRead.Token));
        Assert.True(stream.CanRead);
    }
    [Fact]
    public void Expanded_size_row_and_compression_ratio_limits_are_enforced()
    {
        Assert.Equal("DiagnosticCatalogImport.UnsafeArchive", Assert.Single(LoincPackageParser.Parse(Package(), DiagnosticKind.Lab, "2.83", new() { MaxExpandedBytes = 10 }, TestContext.Current.CancellationToken).Errors).Code);
        Assert.Equal("DiagnosticCatalogImport.UnsafeArchive", Assert.Single(LoincPackageParser.Parse(Package(), DiagnosticKind.Lab, "2.83", new() { MaxRows = 1 }, TestContext.Current.CancellationToken).Errors).Code);
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("Oversized.txt", CompressionLevel.SmallestSize).Open());
            writer.Write(new string('A', 2 * 1024 * 1024));
        }
        Assert.Equal("DiagnosticCatalogImport.UnsafeArchive", Assert.Single(LoincPackageParser.Parse(stream.ToArray(), DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken).Errors).Code);
    }
    private static byte[] RewritePackage(Func<string, string?> path)
    {
        using var source = new MemoryStream(Package());
        using var original = new ZipArchive(source, ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in original.Entries)
            {
                if (path(entry.FullName) is not { } target) continue;
                using var input = entry.Open();
                using var destination = zip.CreateEntry(target).Open();
                input.CopyTo(destination);
            }
        }
        return output.ToArray();
    }
    private sealed class NonseekableStream(byte[] content) : MemoryStream(content, writable: false)
    {
        public override bool CanSeek => false;
    }
    private sealed class CancellingStream(byte[] content, CancellationTokenSource cancellation) : MemoryStream(content, writable: false)
    {
        public override int Read(Span<byte> buffer)
        {
            var count = base.Read(buffer);
            cancellation.Cancel();
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, count);
            cancellation.Cancel();
            return read;
        }
    }
    [Theory]
    [InlineData(DiagnosticKind.Lab)]
    [InlineData(DiagnosticKind.Radiology)]
    public void Header_based_import_filters_and_groups_the_real_source_schema(DiagnosticKind kind)
    {
        var parsed = LoincPackageParser.Parse(Package(), kind, "2.83", new(), TestContext.Current.CancellationToken); Assert.True(parsed.IsSuccess);
        if (kind == DiagnosticKind.Lab)
        {
            Assert.Equal(2, parsed.Value.Count); Assert.True(parsed.Value.Single(r => r.Data.Code == "1234-5").Data.IsCommonOrder);
            Assert.Equal("اسم رسمي", parsed.Value.Single(r => r.Data.Code == "1234-5").Data.OfficialNameAr);
            Assert.Null(parsed.Value.Single(r => r.Data.Code == "2345-6").Data.OfficialNameAr);
        }
        else
        {
            var row = Assert.Single(parsed.Value); Assert.Equal("5678-9", row.Data.Code); Assert.Equal("XR", Assert.Single(row.Data.Attributes["Rad.Modality.Modality type"])); Assert.Equal(3, row.Data.Attributes.Count);
            Assert.Equal("LP1", row.Data.RadiologyParts![0]["PartNumber"]); Assert.Equal("A", row.Data.RadiologyParts[0]["PartSequenceOrder"]);
        }
    }
    [Theory]
    [InlineData("TRIAL")]
    [InlineData("DISCOURAGED")]
    [InlineData("DEPRECATED")]
    public void Nonactive_source_rows_and_partial_arabic_are_retained(string status)
    {
        var parsed = LoincPackageParser.Parse(Package(status: status, arabic: ""), DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken).Value;
        var row = parsed.Single(r => r.Data.Code == "1234-5"); Assert.Equal(status, row.Data.Status); Assert.Null(row.Data.OfficialNameAr);
    }
    [Theory]
    [InlineData("../escape.csv")]
    [InlineData("/absolute.csv")]
    [InlineData("C:/absolute.csv")]
    [InlineData("nested/../../escape.csv")]
    public void Unsafe_zip_paths_are_rejected(string path)
    {
        var result = LoincPackageParser.Parse(Package(path), DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken); Assert.False(result.IsSuccess); Assert.Equal("DiagnosticCatalogImport.UnsafeArchive", result.Errors[0].Code);
    }
    [Fact]
    public void Invalid_archive_headers_version_and_package_limits_are_rejected()
    {
        Assert.Equal("DiagnosticCatalogImport.InvalidArchive", LoincPackageParser.Parse([1, 2, 3], DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken).Errors[0].Code);
        Assert.Equal("DiagnosticCatalogImport.InvalidHeaders", LoincPackageParser.Parse(Package(invalidHeaders: true), DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken).Errors[0].Code);
        Assert.Equal("DiagnosticCatalogImport.UnsupportedSourceVersion", LoincPackageParser.Parse(Package(), DiagnosticKind.Lab, "unknown", new(), TestContext.Current.CancellationToken).Errors[0].Code);
        Assert.Equal("DiagnosticCatalogImport.PackageTooLarge", LoincPackageParser.Parse(Package(), DiagnosticKind.Lab, "2.83", new() { MaxPackageBytes = 10 }, TestContext.Current.CancellationToken).Errors[0].Code);
        Assert.Equal("DiagnosticCatalogImport.UnsafeArchive", LoincPackageParser.Parse(Package(), DiagnosticKind.Lab, "2.83", new() { MaxEntries = 1 }, TestContext.Current.CancellationToken).Errors[0].Code);
        using var stream = new MemoryStream(); using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) zip.CreateEntry("Other.csv");
        Assert.Equal("DiagnosticCatalogImport.RequiredFileMissing", LoincPackageParser.Parse(stream.ToArray(), DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken).Errors[0].Code);
    }
    [Fact]
    public void Csv_supports_quoted_commas_multiline_source_fields_and_preserves_copyright()
    {
        using var stream = new MemoryStream(); using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var table = zip.CreateEntry("LoincTable/Loinc.csv"); using (var writer = new StreamWriter(table.Open())) writer.Write("STATUS,ORDER_OBS,LONG_COMMON_NAME,LOINC_NUM,CLASSTYPE,COMPONENT,EXTERNAL_COPYRIGHT_NOTICE\r\nACTIVE,Both,\"Test, with comma\",1234-5,1,\"Line 1\r\nLine 2\",Original notice\r\n");
            var common = zip.CreateEntry("LoincUniversalLabOrdersValueSet.csv"); using var commonWriter = new StreamWriter(common.Open()); commonWriter.Write("LOINC_NUM\n1234-5\n");
        }
        var parsed = LoincPackageParser.Parse(stream.ToArray(), DiagnosticKind.Lab, "2.83", new(), TestContext.Current.CancellationToken); Assert.True(parsed.IsSuccess);
        var row = Assert.Single(parsed.Value); Assert.Equal("Test, with comma", row.Data.NameEn); Assert.Equal("Line 1\r\nLine 2", row.Data.Fields["COMPONENT"]); Assert.Equal("Original notice", row.Data.Fields["EXTERNAL_COPYRIGHT_NOTICE"]);
    }
}
