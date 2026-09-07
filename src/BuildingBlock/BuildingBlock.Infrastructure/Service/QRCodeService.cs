using BuildingBlock.Application.Abstraction.QrCode;
using BuildingBlock.Application.Diagnostics;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Exceptions;
using BuildingBlock.Infrastructure.Options;
using Microsoft.Extensions.Options;
using QRCoder;
using System.Text;

namespace BuildingBlock.Infrastructure.Service
{
    internal sealed class QRCodeService : IQRCodeService
    {
        private readonly QrCodeOptions _options;

        public QRCodeService(IOptions<QrCodeOptions> options)
        {
            _options = options.Value;
        }

        public GeneratedQrCode Generate(QrCodeRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var format = request.Format.ToString().ToLowerInvariant();

            try
            {
                Validate(request);

                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(
                    request.Payload,
                    MapErrorCorrectionLevel(request.ErrorCorrectionLevel));

                var generated = request.Format switch
                {
                    QrCodeImageFormat.Png => GeneratePng(qrCodeData, request.PixelsPerModule),
                    QrCodeImageFormat.Svg => GenerateSvg(qrCodeData, request.PixelsPerModule),
                    _ => throw InvalidPayload("QR image format is invalid.")
                };

                BuildingBlockDiagnostics.RecordQrCodeGenerated(format);
                return generated;
            }
            catch (QrCodeServiceException exception)
            {
                BuildingBlockDiagnostics.RecordQrCodeFailure(format, exception.Error.Type.ToString());
                throw;
            }
            catch (Exception exception)
            {
                BuildingBlockDiagnostics.RecordQrCodeFailure(format, exception.GetType().Name);
                throw new QrCodeServiceException(
                    Error.Infra(
                        ExternalServiceErrorCodes.QrCode.GenerationFailed,
                        "QR code generation failed.",
                        source: "QrCode"),
                    exception);
            }
        }

        private void Validate(QrCodeRequest request)
        {
            if (string.IsNullOrEmpty(request.Payload))
            {
                throw InvalidPayload("QR payload is required.");
            }

            var payloadBytes = Encoding.UTF8.GetByteCount(request.Payload);
            if (payloadBytes > _options.MaximumPayloadBytes)
            {
                throw new QrCodeServiceException(
                    Error.Validation(
                        ExternalServiceErrorCodes.QrCode.PayloadTooLarge,
                        "QR payload exceeds the configured maximum size.",
                        source: "QrCode"));
            }

            if (request.PixelsPerModule < _options.MinimumPixelsPerModule ||
                request.PixelsPerModule > _options.MaximumPixelsPerModule)
            {
                throw InvalidPayload("QR pixels-per-module value is outside the configured range.");
            }

            if (!Enum.IsDefined(request.ErrorCorrectionLevel))
            {
                throw InvalidPayload("QR error-correction level is invalid.");
            }

            if (!Enum.IsDefined(request.Format))
            {
                throw InvalidPayload("QR image format is invalid.");
            }
        }

        private static GeneratedQrCode GeneratePng(QRCodeData data, int pixelsPerModule)
        {
            var qrCode = new PngByteQRCode(data);
            return new GeneratedQrCode(
                qrCode.GetGraphic(pixelsPerModule),
                "image/png",
                ".png");
        }

        private static GeneratedQrCode GenerateSvg(QRCodeData data, int pixelsPerModule)
        {
            var qrCode = new SvgQRCode(data);
            var svg = qrCode.GetGraphic(pixelsPerModule);
            return new GeneratedQrCode(
                Encoding.UTF8.GetBytes(svg),
                "image/svg+xml",
                ".svg");
        }

        private static QRCodeGenerator.ECCLevel MapErrorCorrectionLevel(QrCodeErrorCorrectionLevel level)
            => level switch
            {
                QrCodeErrorCorrectionLevel.L => QRCodeGenerator.ECCLevel.L,
                QrCodeErrorCorrectionLevel.M => QRCodeGenerator.ECCLevel.M,
                QrCodeErrorCorrectionLevel.Q => QRCodeGenerator.ECCLevel.Q,
                QrCodeErrorCorrectionLevel.H => QRCodeGenerator.ECCLevel.H,
                _ => QRCodeGenerator.ECCLevel.Q
            };

        private static QrCodeServiceException InvalidPayload(string message)
            => new(
                Error.Validation(
                    ExternalServiceErrorCodes.QrCode.InvalidPayload,
                    message,
                    source: "QrCode"));
    }
}
