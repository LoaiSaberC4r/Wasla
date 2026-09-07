using BuildingBlock.Api;
using BuildingBlock.Api.Middleware;
using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Domain.Results;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BuildingBlock.Tests;

public sealed class ProblemDetailsHttpIntegrationTests
{
    [Theory]
    [InlineData("/missing", "GET", HttpStatusCode.NotFound, "Common.NotFound")]
    [InlineData("/method", "POST", HttpStatusCode.MethodNotAllowed, "Common.MethodNotAllowed")]
    [InlineData("/rate-limited", "GET", (HttpStatusCode)429, "Common.RateLimited")]
    [InlineData("/throw", "GET", HttpStatusCode.InternalServerError, "Common.Unknown")]
    public async Task Framework_and_exception_responses_use_problem_json(
        string path,
        string method,
        HttpStatusCode status,
        string errorCode)
    {
        await using var application = await CreateApplicationAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add(CorrelationIdMiddleware.CorrelationHeader, "corr-test");

        using var response = await application.Client.SendAsync(request, TestContext.Current.CancellationToken);
        using var problem = await ReadProblemAsync(response);

        Assert.Equal(status, response.StatusCode);
        AssertProblem(problem.RootElement, (int)status, errorCode, "corr-test");

        if (status == (HttpStatusCode)429)
        {
            Assert.Equal("3", response.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(3, GetFirstError(problem.RootElement).GetProperty("retryAfter").GetDouble());
        }
    }

    [Fact]
    public async Task Existing_framework_error_body_is_not_replaced()
    {
        await using var application = await CreateApplicationAsync();

        using var response = await application.Client.GetAsync("/custom-error", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("custom-error", body);
    }

    [Fact]
    public async Task Empty_429_without_retry_after_remains_a_problem_without_invented_retry_metadata()
    {
        await using var application = await CreateApplicationAsync();

        using var response = await application.Client.GetAsync("/empty-rate-limited", TestContext.Current.CancellationToken);
        using var problem = await ReadProblemAsync(response);

        Assert.Equal((HttpStatusCode)429, response.StatusCode);
        Assert.Null(response.Headers.RetryAfter);
        Assert.Equal(JsonValueKind.Null, GetFirstError(problem.RootElement).GetProperty("retryAfter").ValueKind);
    }

    [Theory]
    [InlineData("Production", false)]
    [InlineData("Development", true)]
    public async Task Unexpected_exception_diagnostics_follow_environment_redaction(string environmentName, bool exposed)
    {
        await using var application = await CreateApplicationAsync(environmentName: environmentName);

        using var response = await application.Client.GetAsync("/throw", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(exposed, body.Contains("boom", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("validation", 422, "Validation.Invalid")]
    [InlineData("domain", 422, "Common.BadRequest")]
    [InlineData("not-found", 404, "Common.NotFound")]
    [InlineData("conflict", 409, "Common.Conflict")]
    [InlineData("infrastructure", 503, "Common.Infrastructure")]
    [InlineData("unknown", 500, "Common.Unknown")]
    [InlineData("rate-limit", 429, "Common.RateLimited")]
    public async Task Minimal_api_failures_use_the_unified_contract(string kind, int status, string errorCode)
    {
        await using var application = await CreateApplicationAsync();

        using var response = await application.Client.GetAsync($"/minimal/failure/{kind}", TestContext.Current.CancellationToken);
        using var problem = await ReadProblemAsync(response);

        Assert.Equal(status, (int)response.StatusCode);
        AssertProblem(problem.RootElement, status, errorCode);

        if (status == 429)
        {
            Assert.Equal(TimeSpan.FromSeconds(4), response.Headers.RetryAfter!.Delta);
        }
    }

    [Fact]
    public async Task Minimal_api_success_preserves_ok_and_no_content_semantics()
    {
        await using var application = await CreateApplicationAsync();

        using var value = await application.Client.GetAsync("/minimal/value", TestContext.Current.CancellationToken);
        using var noContent = await application.Client.GetAsync("/minimal/no-content", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, value.StatusCode);
        Assert.Equal("phase2", await value.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NoContent, noContent.StatusCode);
        Assert.Equal(0, noContent.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task Mvc_success_and_result_failure_use_the_unified_contract()
    {
        await using var application = await CreateApplicationAsync();

        using var value = await application.Client.GetAsync("/mvc/value", TestContext.Current.CancellationToken);
        using var noContent = await application.Client.DeleteAsync("/mvc/value", TestContext.Current.CancellationToken);
        using var failure = await application.Client.GetAsync("/mvc/failure/conflict", TestContext.Current.CancellationToken);
        using var problem = await ReadProblemAsync(failure);

        Assert.Equal(HttpStatusCode.OK, value.StatusCode);
        Assert.Equal("phase2", await value.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NoContent, noContent.StatusCode);
        AssertProblem(problem.RootElement, 409, ErrorCodes.Common.Conflict);
    }

    [Fact]
    public async Task Mvc_automatic_validation_uses_safe_422_error_metadata()
    {
        await using var application = await CreateApplicationAsync();
        using var content = JsonContent.Create(new { age = 5 });

        using var response = await application.Client.PostAsync("/mvc/validate", content, TestContext.Current.CancellationToken);
        using var problem = await ReadProblemAsync(response);
        var error = GetFirstError(problem.RootElement);

        AssertProblem(problem.RootElement, 422, ErrorCodes.Validation.Invalid);
        Assert.StartsWith("field:", error.GetProperty("details").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("age = 5", problem.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mvc_malformed_json_returns_stable_safe_validation_error()
    {
        await using var application = await CreateApplicationAsync();
        using var content = new StringContent("{\"name\":", Encoding.UTF8, "application/json");

        using var response = await application.Client.PostAsync("/mvc/validate", content, TestContext.Current.CancellationToken);
        using var problem = await ReadProblemAsync(response);

        AssertProblem(problem.RootElement, 422, ErrorCodes.Validation.InvalidJson);
        Assert.DoesNotContain("JsonException", problem.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("System.", problem.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mvc_model_binding_failure_returns_stable_safe_error()
    {
        await using var application = await CreateApplicationAsync();

        using var response = await application.Client.GetAsync("/mvc/bind?count=not-a-number", TestContext.Current.CancellationToken);
        using var problem = await ReadProblemAsync(response);

        AssertProblem(problem.RootElement, 422, ErrorCodes.Validation.ModelBinding);
        Assert.DoesNotContain("not-a-number", problem.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mvc_unsupported_content_type_and_controller_exception_use_shared_writer()
    {
        await using var application = await CreateApplicationAsync();
        using var content = new StringContent("name=phase2", Encoding.UTF8, "text/plain");

        using var unsupported = await application.Client.PostAsync("/mvc/validate", content, TestContext.Current.CancellationToken);
        using var unsupportedProblem = await ReadProblemAsync(unsupported);
        using var exception = await application.Client.GetAsync("/mvc/throw", TestContext.Current.CancellationToken);
        using var exceptionProblem = await ReadProblemAsync(exception);

        AssertProblem(unsupportedProblem.RootElement, 415, ErrorCodes.Common.UnsupportedMediaType);
        AssertProblem(exceptionProblem.RootElement, 500, ErrorCodes.Common.Unknown);
    }

    [Fact]
    public async Task Mvc_and_minimal_api_errors_have_the_same_shape()
    {
        await using var application = await CreateApplicationAsync();

        using var minimalResponse = await application.Client.GetAsync("/minimal/failure/validation", TestContext.Current.CancellationToken);
        using var mvcResponse = await application.Client.GetAsync("/mvc/failure/validation", TestContext.Current.CancellationToken);
        using var minimal = await ReadProblemAsync(minimalResponse);
        using var mvc = await ReadProblemAsync(mvcResponse);

        Assert.Equal(GetPropertyNames(minimal.RootElement), GetPropertyNames(mvc.RootElement));
        Assert.Equal(GetPropertyNames(GetFirstError(minimal.RootElement)), GetPropertyNames(GetFirstError(mvc.RootElement)));
    }

    [Fact]
    public async Task Authentication_challenge_preserves_scheme_header_and_problem_body()
    {
        await using var application = await CreateApplicationAsync();

        using var response = await application.Client.GetAsync("/secure", TestContext.Current.CancellationToken);
        using var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == TestAuthenticationHandler.DefaultScheme);
        Assert.Equal(TestAuthenticationHandler.DefaultScheme, response.Headers.GetValues("X-Test-Challenge").Single());
        AssertProblem(problem.RootElement, 401, ErrorCodes.Common.Unauthorized);
        Assert.Equal("Unauthorized", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Authenticated_but_unauthorized_request_uses_forbid_semantics()
    {
        await using var application = await CreateApplicationAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin");
        request.Headers.Add("X-Test-User", "alice");

        using var response = await application.Client.SendAsync(request, TestContext.Current.CancellationToken);
        using var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains(HeaderNames.WWWAuthenticate));
        Assert.Equal(TestAuthenticationHandler.DefaultScheme, response.Headers.GetValues("X-Test-Forbid").Single());
        AssertProblem(problem.RootElement, 403, ErrorCodes.Common.Forbidden);
        Assert.Equal("Security Error", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Explicit_policy_scheme_is_used_for_challenge_and_forbid()
    {
        await using var application = await CreateApplicationAsync();

        using var challenge = await application.Client.GetAsync("/explicit", TestContext.Current.CancellationToken);
        using var challengeProblem = await ReadProblemAsync(challenge);
        using var forbidRequest = new HttpRequestMessage(HttpMethod.Get, "/explicit-admin");
        forbidRequest.Headers.Add("X-Test-User", "alice");
        using var forbid = await application.Client.SendAsync(forbidRequest, TestContext.Current.CancellationToken);
        using var forbidProblem = await ReadProblemAsync(forbid);

        Assert.Equal(TestAuthenticationHandler.ExplicitScheme, challenge.Headers.GetValues("X-Test-Challenge").Single());
        Assert.Contains(challenge.Headers.WwwAuthenticate, value => value.Scheme == TestAuthenticationHandler.ExplicitScheme);
        AssertProblem(challengeProblem.RootElement, 401, ErrorCodes.Common.Unauthorized);
        Assert.Equal(TestAuthenticationHandler.ExplicitScheme, forbid.Headers.GetValues("X-Test-Forbid").Single());
        AssertProblem(forbidProblem.RootElement, 403, ErrorCodes.Common.Forbidden);
    }

    [Fact]
    public async Task Multiple_policy_schemes_are_each_challenged()
    {
        await using var application = await CreateApplicationAsync();

        using var response = await application.Client.GetAsync("/multiple-schemes", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            new[] { TestAuthenticationHandler.DefaultScheme, TestAuthenticationHandler.ExplicitScheme },
            response.Headers.GetValues("X-Test-Challenge"));
        Assert.Equal(2, response.Headers.WwwAuthenticate.Count);
    }

    [Fact]
    public async Task Response_handling_authentication_scheme_is_not_written_twice()
    {
        await using var application = await CreateApplicationAsync();

        using var response = await application.Client.GetAsync("/handled-challenge", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("scheme-handled", body);
    }

    [Fact]
    public async Task Problem_details_writer_respects_consumer_json_configuration()
    {
        await using var application = await CreateApplicationAsync(services =>
            services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower));

        using var response = await application.Client.GetAsync("/minimal/failure/rate-limit", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"retry_after\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"retryAfter\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Problem_details_registration_is_idempotent()
    {
        var services = new ServiceCollection();

        services.AddBuildingBlockProblemDetails();
        services.AddBuildingBlockProblemDetails();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IProblemDetailsMapper));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IBuildingBlockProblemDetailsWriter));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAuthorizationMiddlewareResultHandler));
        Assert.Single(services, descriptor => descriptor.ImplementationType == typeof(ConfigureBuildingBlockApiBehaviorOptions));
    }

    private static Task<BuildingBlockTestApplication> CreateApplicationAsync(
        Action<IServiceCollection>? additionalServices = null,
        string environmentName = "Development")
        => BuildingBlockTestApplication.CreateAsync(
            services =>
            {
                services.AddRouting();
                services.AddControllers().AddApplicationPart(typeof(Phase2ProblemDetailsController).Assembly);
                services.AddApiVersioning(options =>
                {
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.AssumeDefaultVersionWhenUnspecified = true;
                }).AddMvc();
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.DefaultScheme;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.DefaultScheme;
                        options.DefaultForbidScheme = TestAuthenticationHandler.DefaultScheme;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.DefaultScheme,
                        _ => { })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.ExplicitScheme,
                        _ => { })
                    .AddScheme<AuthenticationSchemeOptions, ResponseHandlingAuthenticationHandler>(
                        ResponseHandlingAuthenticationHandler.SchemeName,
                        _ => { });
                services.AddAuthorization(options =>
                {
                    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
                    options.AddPolicy("Explicit", policy =>
                    {
                        policy.AuthenticationSchemes.Add(TestAuthenticationHandler.ExplicitScheme);
                        policy.RequireAuthenticatedUser();
                    });
                    options.AddPolicy("ExplicitAdmin", policy =>
                    {
                        policy.AuthenticationSchemes.Add(TestAuthenticationHandler.ExplicitScheme);
                        policy.RequireRole("Admin");
                    });
                    options.AddPolicy("MultipleSchemes", policy =>
                    {
                        policy.AuthenticationSchemes.Add(TestAuthenticationHandler.DefaultScheme);
                        policy.AuthenticationSchemes.Add(TestAuthenticationHandler.ExplicitScheme);
                        policy.RequireAuthenticatedUser();
                    });
                    options.AddPolicy("HandledChallenge", policy =>
                    {
                        policy.AuthenticationSchemes.Add(ResponseHandlingAuthenticationHandler.SchemeName);
                        policy.RequireAuthenticatedUser();
                    });
                });
                services.AddBuildingBlockProblemDetails();
                additionalServices?.Invoke(services);
            },
            app =>
            {
                app.UseMiddleware<CorrelationIdMiddleware>();
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseBuildingBlockProblemDetailsStatusCodes();
                app.MapControllers();
                MapEndpoints(app);
            },
            environmentName);

    private static void MapEndpoints(WebApplication endpoints)
    {
        endpoints.MapGet("/secure", () => Results.Ok()).RequireAuthorization();
        endpoints.MapGet("/admin", () => Results.Ok()).RequireAuthorization("AdminOnly");
        endpoints.MapGet("/explicit", () => Results.Ok()).RequireAuthorization("Explicit");
        endpoints.MapGet("/explicit-admin", () => Results.Ok()).RequireAuthorization("ExplicitAdmin");
        endpoints.MapGet("/multiple-schemes", () => Results.Ok()).RequireAuthorization("MultipleSchemes");
        endpoints.MapGet("/handled-challenge", () => Results.Ok()).RequireAuthorization("HandledChallenge");
        endpoints.MapGet("/method", () => Results.Ok());
        endpoints.MapGet("/rate-limited", context =>
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "3";
            return Task.CompletedTask;
        });
        endpoints.MapGet("/empty-rate-limited", context =>
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return Task.CompletedTask;
        });
        endpoints.MapGet("/custom-error", async context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("custom-error", context.RequestAborted);
        });
        endpoints.MapGet("/throw", (HttpContext _) =>
        {
            throw new InvalidOperationException("boom");
        });
        endpoints.MapGet("/minimal/value", (HttpContext http) => Result<string>.Ok("phase2").ToIResult(http));
        endpoints.MapGet("/minimal/no-content", (HttpContext http) => Result.Ok().ToIResult(http));
        endpoints.MapGet("/minimal/failure/{kind}", (string kind, HttpContext http) =>
            Result<string>.Fail(CreateError(kind)).ToIResult(http));
    }

    private static Error CreateError(string kind)
        => kind switch
        {
            "validation" => Error.Validation(ErrorCodes.Validation.Invalid, "Invalid input."),
            "domain" => Error.Domain(ErrorCodes.Common.BadRequest, "Domain failure."),
            "not-found" => Error.NotFound(ErrorCodes.Common.NotFound, "Missing."),
            "conflict" => Error.Conflict(ErrorCodes.Common.Conflict, "Conflict."),
            "infrastructure" => Error.Infra(ErrorCodes.Common.Infrastructure, "Dependency secret."),
            "unknown" => Error.Unknown(ErrorCodes.Common.Unknown, "Unknown secret."),
            "rate-limit" => Error.RateLimit(ErrorCodes.Common.RateLimited, "Slow down.", TimeSpan.FromSeconds(4)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported test error kind.")
        };

    private static async Task<JsonDocument> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static void AssertProblem(JsonElement problem, int status, string errorCode, string? correlationId = null)
    {
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        Assert.StartsWith("/", problem.GetProperty("instance").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.Equal(errorCode, GetFirstError(problem).GetProperty("code").GetString());

        if (correlationId is not null)
        {
            Assert.Equal(correlationId, problem.GetProperty("correlationId").GetString());
        }
    }

    private static JsonElement GetFirstError(JsonElement problem)
        => problem.GetProperty("errors").EnumerateArray().First();

    private static string[] GetPropertyNames(JsonElement element)
        => element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string DefaultScheme = "Test";
        public const string ExplicitScheme = "Explicit";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var user))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(ClaimTypes.Name, user.ToString()) };
            if (Request.Headers.TryGetValue("X-Test-Role", out var role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.Headers.Append(HeaderNames.WWWAuthenticate, $"{Scheme.Name} realm=\"buildingblock-tests\"");
            Response.Headers.Append("X-Test-Challenge", Scheme.Name);
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            Response.Headers.Append("X-Test-Forbid", Scheme.Name);
            return Task.CompletedTask;
        }
    }

    private sealed class ResponseHandlingAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Handled";

        public ResponseHandlingAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.ContentType = "text/plain";
            await Response.WriteAsync("scheme-handled", Context.RequestAborted);
        }
    }
}

[ApiController]
[Route("mvc")]
public sealed class Phase2ProblemDetailsController : ControllerBase
{
    [HttpGet("value")]
    public IActionResult GetValue()
        => Result<string>.Ok("phase2").ToIActionResult();

    [HttpDelete("value")]
    public IActionResult DeleteValue()
        => Result.Ok().ToIActionResult();

    [HttpGet("failure/{kind}")]
    public IActionResult GetFailure(string kind)
        => Result<string>.Fail(kind switch
        {
            "validation" => Error.Validation(ErrorCodes.Validation.Invalid, "Invalid input."),
            "conflict" => Error.Conflict(ErrorCodes.Common.Conflict, "Conflict."),
            _ => Error.Unknown(ErrorCodes.Common.Unknown, "Unknown.")
        }).ToIActionResult();

    [HttpPost("validate")]
    [Consumes("application/json")]
    public IActionResult Validate([FromBody] Phase2Payload payload)
        => Ok(payload);

    [HttpGet("bind")]
    public IActionResult Bind([FromQuery] int count)
        => Ok(count);

    [HttpGet("throw")]
    public IActionResult Throw()
        => throw new InvalidOperationException("controller secret");
}

public sealed class Phase2Payload
{
    [Required]
    public string? Name { get; init; }

    public int Age { get; init; }
}
