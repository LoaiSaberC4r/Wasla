using System.Buffers.Binary;
using System.IO.Compression;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

// Adapted from CustomerSurvey's bounded PNG chart approach; values already come from the shared report model.
internal static class ReportChartPngRenderer
{
    public static byte[] Render(IReadOnlyList<decimal> percentages)
    {
        const int width = 600; var height = Math.Max(80, percentages.Count * 34 + 20);
        var scanlines = new byte[height * (1 + width * 3)];
        for (var y = 0; y < height; ++y)
        for (var x = 0; x < width; ++x)
        {
            var offset = y * (1 + width * 3) + 1 + x * 3;
            var index = (y - 10) / 34;
            var inBar = y >= 10 && index < percentages.Count && (y - 10) % 34 < 22 && x >= 4 && x < width - 4;
            var filled = inBar && x - 4 < (width - 8) * Math.Clamp(percentages[index], 0, 100) / 100;
            scanlines[offset] = filled ? (byte)20 : inBar ? (byte)224 : (byte)255;
            scanlines[offset + 1] = filled ? (byte)121 : inBar ? (byte)237 : (byte)255;
            scanlines[offset + 2] = filled ? (byte)143 : inBar ? (byte)241 : (byte)255;
        }
        using var output = new MemoryStream(); output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header, width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; header[9] = 2; Chunk(output, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(scanlines);
        Chunk(output, "IDAT", compressed.ToArray()); Chunk(output, "IEND", []); return output.ToArray();
    }
    private static void Chunk(Stream stream, string type, byte[] bytes)
    {
        Span<byte> integer = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(integer, bytes.Length); stream.Write(integer);
        var tag = System.Text.Encoding.ASCII.GetBytes(type); stream.Write(tag); stream.Write(bytes);
        uint crc = uint.MaxValue;
        foreach (var b in tag.Concat(bytes)) { crc ^= b; for (var i = 0; i < 8; ++i) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320U : 0); }
        BinaryPrimitives.WriteUInt32BigEndian(integer, crc ^ uint.MaxValue); stream.Write(integer);
    }
}
