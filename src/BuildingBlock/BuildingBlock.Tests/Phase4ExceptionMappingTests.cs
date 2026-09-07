using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace BuildingBlock.Tests;

public sealed class Phase4ExceptionMappingTests
{
    [Fact]
    public void External_service_exceptions_map_to_safe_errors()
    {
        IExceptionToErrorMapper mapper = new ExternalServiceExceptionToErrorMapper();

        Assert.True(mapper.TryMap(
            new MediaServiceException(Error.Validation(ExternalServiceErrorCodes.Media.InvalidFile, "Invalid file.")),
            out var mediaInput));
        Assert.True(mapper.TryMap(
            new MediaServiceException(Error.Infra(ExternalServiceErrorCodes.Media.StorageUnavailable, "Storage unavailable.")),
            out var mediaStorage));
        Assert.True(mapper.TryMap(
            new EmailServiceException(Error.Validation(ExternalServiceErrorCodes.Email.InvalidMessage, "Invalid email.")),
            out var emailInput));
        Assert.True(mapper.TryMap(
            new EmailServiceException(Error.Infra(ExternalServiceErrorCodes.Email.SendFailed, "SMTP unavailable.")),
            out var emailFailure));
        Assert.True(mapper.TryMap(
            new QrCodeServiceException(Error.Validation(ExternalServiceErrorCodes.QrCode.InvalidPayload, "Invalid QR.")),
            out var qrInput));

        Assert.Equal(ErrorType.Validation, mediaInput.Type);
        Assert.Equal(ErrorType.Infrastructure, mediaStorage.Type);
        Assert.Equal(ErrorType.Validation, emailInput.Type);
        Assert.Equal(ErrorType.Infrastructure, emailFailure.Type);
        Assert.Equal(ErrorType.Validation, qrInput.Type);
    }

    [Fact]
    public void Infrastructure_problem_details_hide_internal_messages_in_production()
    {
        var mapper = new ProblemDetailsMapper(new TestEnvironment { EnvironmentName = Environments.Production });
        var problem = mapper.Map(
            new DefaultHttpContext(),
            new[]
            {
                Error.Infra(ExternalServiceErrorCodes.Email.SendFailed, "SMTP said password was bad.", source: "Email")
            });

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
        Assert.Equal("The request could not be completed.", problem.Detail);
        Assert.DoesNotContain("password", problem.Extensions["errors"]!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";

        public IFileProvider WebRootFileProvider { get; set; } = null!;

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = Environments.Production;

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
