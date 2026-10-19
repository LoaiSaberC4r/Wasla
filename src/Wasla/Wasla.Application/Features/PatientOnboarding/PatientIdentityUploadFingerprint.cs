using System.Security.Cryptography;
using BuildingBlock.Application.Abstraction.Media;
namespace Wasla.Application.Features.PatientOnboarding;
public static class PatientIdentityUploadFingerprint
{
    public static async Task<string?> HashAsync(MediaUpload? file, CancellationToken ct)
    {
        if (file is null) return null;
        if (!file.Content.CanSeek) throw new ArgumentException("A replayable upload stream is required.");
        var original = file.Content.Position;
        try { return Convert.ToHexString(await SHA256.HashDataAsync(file.Content, ct)); }
        finally { file.Content.Position = original; }
    }
}
