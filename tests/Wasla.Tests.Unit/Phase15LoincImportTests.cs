using System.IO.Compression;
using System.Text;
using Wasla.Application.Features.Diagnostics;
using Wasla.Domain.Diagnostics;

namespace Wasla.Tests.Unit;

public sealed class Phase15LoincImportTests
{
    public static byte[] Package(string? extraPath = null, bool invalidHeaders = false, string status = "ACTIVE", string arabic = "اسم رسمي")
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void File(string path, string text)
            {
                var entry = zip.CreateEntry(path); using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)); writer.Write(text);
            }
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
            if (extraPath is not null) File(extraPath, "unsafe");
        }
        return stream.ToArray();
    }
    [Theory]
    [InlineData(DiagnosticKind.Lab)]
    [InlineData(DiagnosticKind.Radiology)]
    public void Header_based_import_filters_and_groups_the_real_source_schema(DiagnosticKind kind)
    {
        var parsed = LoincPackageParser.Parse(Package(), kind, "2.83", new()); Assert.True(parsed.IsSuccess);
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
        var parsed = LoincPackageParser.Parse(Package(status: status, arabic: ""), DiagnosticKind.Lab, "2.83", new()).Value;
        var row = parsed.Single(r => r.Data.Code == "1234-5"); Assert.Equal(status, row.Data.Status); Assert.Null(row.Data.OfficialNameAr);
    }
    [Theory]
    [InlineData("../escape.csv")]
    [InlineData("/absolute.csv")]
    [InlineData("C:/absolute.csv")]
    [InlineData("nested/../../escape.csv")]
    public void Unsafe_zip_paths_are_rejected(string path)
    {
        var result = LoincPackageParser.Parse(Package(path), DiagnosticKind.Lab, "2.83", new()); Assert.False(result.IsSuccess); Assert.Equal("DiagnosticCatalogImport.UnsafeArchive", result.Errors[0].Code);
    }
    [Fact]
    public void Invalid_archive_headers_version_and_package_limits_are_rejected()
    {
        Assert.Equal("DiagnosticCatalogImport.InvalidArchive", LoincPackageParser.Parse([1, 2, 3], DiagnosticKind.Lab, "2.83", new()).Errors[0].Code);
        Assert.Equal("DiagnosticCatalogImport.InvalidHeaders", LoincPackageParser.Parse(Package(invalidHeaders: true), DiagnosticKind.Lab, "2.83", new()).Errors[0].Code);
        Assert.Equal("DiagnosticCatalogImport.UnsupportedSourceVersion", LoincPackageParser.Parse(Package(), DiagnosticKind.Lab, "unknown", new()).Errors[0].Code);
        Assert.Equal("DiagnosticCatalogImport.PackageTooLarge", LoincPackageParser.Parse(Package(), DiagnosticKind.Lab, "2.83", new() { MaxPackageBytes = 10 }).Errors[0].Code);
        Assert.Equal("DiagnosticCatalogImport.UnsafeArchive", LoincPackageParser.Parse(Package(), DiagnosticKind.Lab, "2.83", new() { MaxEntries = 1 }).Errors[0].Code);
        using var stream = new MemoryStream(); using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) zip.CreateEntry("Other.csv");
        Assert.Equal("DiagnosticCatalogImport.RequiredFileMissing", LoincPackageParser.Parse(stream.ToArray(), DiagnosticKind.Lab, "2.83", new()).Errors[0].Code);
    }
    [Fact]
    public void Csv_supports_quoted_commas_multiline_source_fields_and_preserves_copyright()
    {
        using var stream = new MemoryStream(); using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var table = zip.CreateEntry("LoincTable/Loinc.csv"); using (var writer = new StreamWriter(table.Open())) writer.Write("STATUS,ORDER_OBS,LONG_COMMON_NAME,LOINC_NUM,CLASSTYPE,COMPONENT,EXTERNAL_COPYRIGHT_NOTICE\r\nACTIVE,Both,\"Test, with comma\",1234-5,1,\"Line 1\r\nLine 2\",Original notice\r\n");
            var common = zip.CreateEntry("LoincUniversalLabOrdersValueSet.csv"); using var commonWriter = new StreamWriter(common.Open()); commonWriter.Write("LOINC_NUM\n1234-5\n");
        }
        var parsed = LoincPackageParser.Parse(stream.ToArray(), DiagnosticKind.Lab, "2.83", new()); Assert.True(parsed.IsSuccess);
        var row = Assert.Single(parsed.Value); Assert.Equal("Test, with comma", row.Data.NameEn); Assert.Equal("Line 1\r\nLine 2", row.Data.Fields["COMPONENT"]); Assert.Equal("Original notice", row.Data.Fields["EXTERNAL_COPYRIGHT_NOTICE"]);
    }
}
