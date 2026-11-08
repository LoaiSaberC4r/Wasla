using System.Buffers.Binary;
using System.Globalization;
using System.Text;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Media;

/// <summary>Conservative passive PDF / bounded image policy, followed by signature validation and malware scanning.</summary>
public static class HistoricalDocumentSafety
{
    private static readonly HashSet<string> ForbiddenPdfNames = new(StringComparer.OrdinalIgnoreCase)
    { "JavaScript", "JS", "OpenAction", "AA", "Launch", "EmbeddedFile", "EmbeddedFiles", "Filespec", "FileAttachment", "RichMedia", "XFA", "Encrypt", "ObjStm", "AcroForm", "URI", "SubmitForm", "ImportData", "GoToR", "GoToE", "Movie", "Sound", "Rendition" };
    public static async Task<bool> IsPassiveAsync(Stream stream, string extension, CancellationToken ct)
    {
        if (!stream.CanSeek || stream.Length is <= 0 or > 10485760) return false;
        stream.Position = 0;
        try
        {
            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return await PassivePdfAsync(stream, ct);
            var header = new byte[24]; var count = await stream.ReadAsync(header, ct);
            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                if (count < 24 || !header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || !header.AsSpan(12, 4).SequenceEqual("IHDR"u8)) return false;
                return Dimensions(BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16, 4)), BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20, 4)));
            }
            if (count < 3 || header[0] != 255 || header[1] != 216 || header[2] != 255) return false;
            stream.Position = 2;
            // Walk bounded JPEG marker segments; never decode or allocate the image.
            var two = new byte[2]; var frame = new byte[5];
            while (stream.Position < stream.Length)
            {
                if (stream.ReadByte() != 255) return false;
                int marker; do { marker = stream.ReadByte(); } while (marker == 255);
                if (marker is < 0 or 217 or 218) return false;
                if (marker is 1 or >= 208 and <= 215) continue;
                await stream.ReadExactlyAsync(two, ct); var length = BinaryPrimitives.ReadUInt16BigEndian(two);
                if (length < 2 || stream.Position + length - 2 > stream.Length) return false;
                if (marker is >= 192 and <= 195 or >= 197 and <= 199 or >= 201 and <= 203 or >= 205 and <= 207)
                { if (length < 7) return false; await stream.ReadExactlyAsync(frame, ct); return Dimensions(BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(3, 2)), BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(1, 2))); }
                stream.Seek(length - 2, SeekOrigin.Current);
            }
            return false;
        }
        catch (EndOfStreamException) { return false; }
        finally { stream.Position = 0; }
    }
    private static bool Dimensions(uint width, uint height) => width is > 0 and <= 16384 && height is > 0 and <= 16384 && (long)width * height <= 40000000;
    private static async Task<bool> PassivePdfAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[8]; await stream.ReadExactlyAsync(header, ct);
        if (!header.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) return false;
        stream.Position = Math.Max(0, stream.Length - 1024); var tail = new byte[(int)(stream.Length - stream.Position)]; await stream.ReadExactlyAsync(tail, ct);
        if (!Encoding.ASCII.GetString(tail).Contains("%%EOF", StringComparison.Ordinal)) return false;
        stream.Position = 0; var bytes = new byte[8192]; var name = new StringBuilder(); var inName = false;
        while (true)
        {
            var count = await stream.ReadAsync(bytes, ct); if (count == 0) break;
            for (var i = 0; i < count; i++)
            {
                var ch = (char)bytes[i];
                if (ch == '/') { if (inName && Forbidden(name)) return false; name.Clear(); inName = true; }
                else if (char.IsWhiteSpace(ch) || ch is '<' or '>' or '[' or ']' or '(' or ')' or '{' or '}' or '%' || ch == 0)
                { if (inName && Forbidden(name)) return false; name.Clear(); inName = false; }
                else if (inName) { if (name.Length >= 256) return false; name.Append(ch); }
            }
        }
        return !inName || !Forbidden(name);
    }
    private static bool Forbidden(StringBuilder token)
    {
        var value = token.ToString(); var decoded = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '#')
            { if (i + 2 >= value.Length || !byte.TryParse(value.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)) return true; decoded.Append((char)code); i += 2; }
            else decoded.Append(value[i]);
        }
        return ForbiddenPdfNames.Contains(decoded.ToString());
    }
}
