using System.Buffers.Binary;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Reports;

public static class ReportLogoPolicy
{
    public static bool IsBoundedImage(ReadOnlySpan<byte> bytes, string contentType)
    {
        if (bytes.Length > 1024 * 1024) return false;
        if (contentType == "image/png")
            return bytes.Length >= 33 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
                bytes.Slice(12, 4).SequenceEqual("IHDR"u8) &&
                Valid(BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4)), BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4)));
        if (contentType != "image/jpeg" || bytes.Length < 4 || bytes[0] != 255 || bytes[1] != 216) return false;
        for (var offset = 2; offset + 3 < bytes.Length;)
        {
            if (bytes[offset++] != 255) return false;
            while (offset < bytes.Length && bytes[offset] == 255) ++offset;
            if (offset >= bytes.Length) return false;
            var marker = bytes[offset++];
            if (marker is 0xD9 or 0xDA) return false;
            if (marker is 0x01 or >= 0xD0 and <= 0xD7) continue;
            if (offset + 2 > bytes.Length) return false;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
            if (length < 2 || offset + length > bytes.Length) return false;
            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
                return length >= 8 && Valid(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2)),
                    BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2)));
            offset += length;
        }
        return false;
    }
    private static bool Valid(uint width, uint height) => width is > 0 and <= 4096 && height is > 0 and <= 4096 && (long)width * height <= 4_000_000;
}
