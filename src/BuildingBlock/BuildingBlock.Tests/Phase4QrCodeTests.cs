using BuildingBlock.Application.Abstraction.QrCode;
using BuildingBlock.Infrastructure.Exceptions;
using BuildingBlock.Infrastructure.Options;
using BuildingBlock.Infrastructure.Service;
using Microsoft.Extensions.Options;
using System.Text;

namespace BuildingBlock.Tests;

public sealed class Phase4QrCodeTests
{
    [Fact]
    public void Generates_png_and_svg_with_correct_metadata()
    {
        var service = CreateService();

        var png = service.Generate(new QrCodeRequest("payload", QrCodeImageFormat.Png));
        var svg = service.Generate(new QrCodeRequest("payload", QrCodeImageFormat.Svg));

        Assert.Equal("image/png", png.ContentType);
        Assert.Equal(".png", png.FileExtension);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Content.Take(4).ToArray());
        Assert.Equal("image/svg+xml", svg.ContentType);
        Assert.Equal(".svg", svg.FileExtension);
        Assert.Contains("<svg", Encoding.UTF8.GetString(svg.Content), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Enforces_payload_and_pixels_per_module_limits()
    {
        var service = CreateService(options =>
        {
            options.MaximumPayloadBytes = 4;
            options.MinimumPixelsPerModule = 2;
            options.MaximumPixelsPerModule = 8;
        });

        Assert.Throws<QrCodeServiceException>(() =>
            service.Generate(new QrCodeRequest("12345")));
        Assert.Throws<QrCodeServiceException>(() =>
            service.Generate(new QrCodeRequest("1234", PixelsPerModule: 1)));
        Assert.Throws<QrCodeServiceException>(() =>
            service.Generate(new QrCodeRequest("1234", PixelsPerModule: 9)));
    }

    [Fact]
    public void Supports_multibyte_utf8_and_json_helper()
    {
        var service = CreateService(options => options.MaximumPayloadBytes = 64);

        var generated = service.Generate(QrCodeRequest.FromJson(new { Text = "مرحبا" }));

        Assert.NotEmpty(generated.Content);
        Assert.Equal("image/png", generated.ContentType);
    }

    [Theory]
    [InlineData(QrCodeErrorCorrectionLevel.L)]
    [InlineData(QrCodeErrorCorrectionLevel.M)]
    [InlineData(QrCodeErrorCorrectionLevel.Q)]
    [InlineData(QrCodeErrorCorrectionLevel.H)]
    public void Supports_every_error_correction_level(QrCodeErrorCorrectionLevel level)
    {
        var service = CreateService();

        var generated = service.Generate(new QrCodeRequest("payload", ErrorCorrectionLevel: level));

        Assert.NotEmpty(generated.Content);
    }

    private static QRCodeService CreateService(Action<QrCodeOptions>? configure = null)
    {
        var options = new QrCodeOptions();
        configure?.Invoke(options);
        return new QRCodeService(Options.Create(options));
    }
}
