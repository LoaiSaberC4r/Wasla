using System.Text.Json;

namespace BuildingBlock.Application.Abstraction.QrCode
{
    public enum QrCodeImageFormat
    {
        Png,
        Svg
    }

    public enum QrCodeErrorCorrectionLevel
    {
        L,
        M,
        Q,
        H
    }

    public sealed record QrCodeRequest(
        string Payload,
        QrCodeImageFormat Format = QrCodeImageFormat.Png,
        QrCodeErrorCorrectionLevel ErrorCorrectionLevel = QrCodeErrorCorrectionLevel.Q,
        int PixelsPerModule = 20)
    {
        public static QrCodeRequest FromJson<T>(
            T value,
            QrCodeImageFormat format = QrCodeImageFormat.Png,
            QrCodeErrorCorrectionLevel errorCorrectionLevel = QrCodeErrorCorrectionLevel.Q,
            int pixelsPerModule = 20,
            JsonSerializerOptions? options = null)
            => new(
                JsonSerializer.Serialize(value, options),
                format,
                errorCorrectionLevel,
                pixelsPerModule);
    }

    public sealed record GeneratedQrCode(
        byte[] Content,
        string ContentType,
        string FileExtension);

    public interface IQRCodeService
    {
        GeneratedQrCode Generate(QrCodeRequest request);
    }
}
