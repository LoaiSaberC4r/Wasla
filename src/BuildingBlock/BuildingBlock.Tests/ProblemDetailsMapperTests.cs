using BuildingBlock.Api.Middleware;
using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;

namespace BuildingBlock.Tests;

public sealed class ProblemDetailsMapperTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 422)]
    [InlineData(ErrorType.Domain, 422)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Security, 403)]
    [InlineData(ErrorType.RateLimit, 429)]
    [InlineData(ErrorType.Infrastructure, 503)]
    [InlineData(ErrorType.Unknown, 500)]
    public void Maps_error_types_to_status_codes(ErrorType type, int statusCode)
    {
        var context = new DefaultHttpContext();
        var mapper = new ProblemDetailsMapper(new FakeEnvironment("Production"));

        var problem = mapper.Map(context, new[] { CreateError(type) });

        Assert.Equal(statusCode, problem.Status);
    }

    [Fact]
    public void Production_hides_infrastructure_and_unknown_details()
    {
        var context = new DefaultHttpContext();
        var mapper = new ProblemDetailsMapper(new FakeEnvironment("Production"));

        var problem = mapper.Map(context, new[]
        {
            Error.Infra(ErrorCodes.Common.Infrastructure, "SQL timeout on server secret-db", details: "stack"),
            Error.Unknown(ErrorCodes.Common.Unknown, "secret exception")
        });
        var json = JsonSerializer.Serialize(problem);

        Assert.DoesNotContain("secret-db", json);
        Assert.DoesNotContain("secret exception", json);
        Assert.Equal("The request could not be completed.", problem.Detail);
    }

    [Fact]
    public void Development_exposes_safe_details()
    {
        var context = new DefaultHttpContext();
        var mapper = new ProblemDetailsMapper(new FakeEnvironment("Development"));

        var problem = mapper.Map(context, new[]
        {
            Error.Infra(ErrorCodes.Common.Infrastructure, "Dependency failed.", details: "timeout")
        });

        Assert.Equal("Dependency failed.", problem.Detail);
        Assert.Contains("timeout", JsonSerializer.Serialize(problem));
    }

    [Fact]
    public void Adds_trace_correlation_and_retry_after()
    {
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.CorrelationItemKey] = "corr-1";
        var mapper = new ProblemDetailsMapper(new FakeEnvironment("Production"));

        var problem = mapper.Map(context, new[]
        {
            Error.RateLimit(ErrorCodes.Common.RateLimited, "Slow down.", TimeSpan.FromSeconds(7))
        });

        Assert.True(problem.Extensions.ContainsKey("traceId"));
        Assert.Equal("corr-1", problem.Extensions["correlationId"]);
        Assert.Equal("7", context.Response.Headers.RetryAfter);
    }

    [Fact]
    public void Mixed_errors_use_documented_precedence_and_preserve_all_errors()
    {
        var context = new DefaultHttpContext();
        var mapper = new ProblemDetailsMapper(new FakeEnvironment("Production"));

        var problem = mapper.Map(context, new[]
        {
            Error.Validation(ErrorCodes.Validation.Invalid, "Invalid."),
            Error.Unknown(ErrorCodes.Common.Unknown, "Unknown.")
        });

        Assert.Equal(500, problem.Status);
        Assert.Contains("Validation.Invalid", JsonSerializer.Serialize(problem));
        Assert.Contains("Common.Unknown", JsonSerializer.Serialize(problem));
    }

    [Theory]
    [InlineData(ErrorCodes.Common.MethodNotAllowed, 405, "Method Not Allowed")]
    [InlineData(ErrorCodes.Common.UnsupportedMediaType, 415, "Unsupported Media Type")]
    public void Maps_special_framework_error_codes(string code, int status, string title)
    {
        var context = new DefaultHttpContext();
        var mapper = new ProblemDetailsMapper(new FakeEnvironment("Production"));

        var problem = mapper.Map(context, new[] { Error.Domain(code, "Framework error.") });

        Assert.Equal(status, problem.Status);
        Assert.Equal(title, problem.Title);
    }

    [Fact]
    public void Empty_error_collection_maps_to_safe_unknown_problem()
    {
        var context = new DefaultHttpContext();
        var mapper = new ProblemDetailsMapper(new FakeEnvironment("Production"));

        var problem = mapper.Map(context, Array.Empty<Error>());
        var json = JsonSerializer.Serialize(problem);

        Assert.Equal(500, problem.Status);
        Assert.Contains(ErrorCodes.Common.Unknown, json);
        Assert.DoesNotContain("System.", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Exception_mapping_redacts_production_and_exposes_development_diagnostics()
    {
        var exception = new InvalidOperationException("secret diagnostic");
        var productionContext = new DefaultHttpContext();
        var developmentContext = new DefaultHttpContext();

        var production = new ProblemDetailsMapper(new FakeEnvironment("Production"))
            .MapException(productionContext, exception);
        var development = new ProblemDetailsMapper(new FakeEnvironment("Development"))
            .MapException(developmentContext, exception);

        Assert.Equal("An unexpected error occurred.", production.Detail);
        Assert.DoesNotContain("secret diagnostic", JsonSerializer.Serialize(production), StringComparison.Ordinal);
        Assert.Contains("secret diagnostic", development.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Long_error_messages_and_diagnostics_are_bounded()
    {
        var context = new DefaultHttpContext();
        var mapper = new ProblemDetailsMapper(new FakeEnvironment("Development"));
        var longMessage = new string('x', 1500);

        var resultProblem = mapper.Map(context, new[] { Error.Validation(ErrorCodes.Validation.Invalid, longMessage) });
        var exceptionProblem = mapper.MapException(context, new InvalidOperationException(longMessage));

        Assert.Equal(1000, resultProblem.Detail!.Length);
        Assert.Equal(1000, exceptionProblem.Detail!.Length);
        Assert.DoesNotContain(new string('x', 1001), JsonSerializer.Serialize(resultProblem), StringComparison.Ordinal);
    }

    [Fact]
    public void Null_error_collection_is_rejected()
    {
        var mapper = new ProblemDetailsMapper(new FakeEnvironment("Production"));

        Assert.Throws<ArgumentNullException>(() => mapper.Map(new DefaultHttpContext(), null!));
    }

    private static Error CreateError(ErrorType type)
        => type switch
        {
            ErrorType.Validation => Error.Validation(ErrorCodes.Validation.Invalid, "Invalid."),
            ErrorType.Domain => Error.Domain(ErrorCodes.Common.BadRequest, "Domain."),
            ErrorType.NotFound => Error.NotFound(ErrorCodes.Common.NotFound, "Missing."),
            ErrorType.Conflict => Error.Conflict(ErrorCodes.Common.Conflict, "Conflict."),
            ErrorType.Unauthorized => Error.Unauthorized(ErrorCodes.Common.Unauthorized, "Unauthorized."),
            ErrorType.Security => Error.Security(ErrorCodes.Common.Forbidden, "Forbidden."),
            ErrorType.RateLimit => Error.RateLimit(ErrorCodes.Common.RateLimited, "Slow down.", TimeSpan.FromSeconds(1)),
            ErrorType.Infrastructure => Error.Infra(ErrorCodes.Common.Infrastructure, "Infra."),
            _ => Error.Unknown(ErrorCodes.Common.Unknown, "Unknown.")
        };

    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public FakeEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Tests";

        public string WebRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
