using BuildingBlock.Api.Logging;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Infrastructure.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlock.Tests;

public sealed class Phase3SecurityAndDiagnosticsTests
{
    [Fact]
    public async Task Password_hashing_verification_rehash_and_cancellation_are_safe()
    {
        var service = new PasswordService(Options.Create(new PasswordPolicyOptions
        {
            RequiredLength = 8,
            MaximumLength = 32,
            RequireDigit = true,
            RequireUppercase = true,
            RequireLowercase = true,
            RequireNonAlphanumeric = true
        }));
        const string password = "Valid!Password1";

        var hash = service.Hash(password);
        Assert.NotEqual(password, hash);
        Assert.True(service.Verify(password, hash));
        Assert.False(service.Verify("Wrong!Password1", hash));
        Assert.False(service.Verify(password, "not-a-password-hash"));
        Assert.True(service.IsStrongPassword(password));
        Assert.False(service.IsStrongPassword("weak"));
        Assert.Throws<ArgumentException>(() => service.Hash(string.Empty));
        Assert.Throws<ArgumentException>(() => service.Hash(new string('a', 33)));

        var legacyHasher = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2
        }));
        var legacyHash = legacyHasher.HashPassword(null!, password);
        var legacyVerification = service.VerifyDetailed(password, legacyHash);
        Assert.True(legacyVerification.IsValid);
        Assert.True(legacyVerification.SuccessRehashNeeded);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.HashAsync(password, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.VerifyAsync(password, hash, cancellation.Token));
    }

    [Fact]
    public void Redaction_masks_cycles_throwing_getters_and_does_not_mutate_input()
    {
        var policy = new DefaultLogRedactionPolicy();
        var input = new RedactionProbe
        {
            Name = "safe",
            Email = "person@example.test"
        };
        input.Next = input;

        var redacted = Assert.IsType<Dictionary<string, object?>>(policy.Redact("Request", input).Value);

        Assert.Equal("safe", input.Name);
        Assert.Equal("person@example.test", input.Email);
        Assert.Same(input, input.Next);
        Assert.Equal("***", redacted[nameof(RedactionProbe.Email)]);
        Assert.Equal("***", redacted[nameof(RedactionProbe.Next)]);
        Assert.Equal("***", redacted[nameof(RedactionProbe.Throwing)]);
    }

    [Fact]
    public async Task Logging_diagnostics_are_disabled_by_default_in_production_and_authorized_when_enabled()
    {
        await using var disabled = BuildDiagnosticsApplication(enabled: null);
        disabled.MapBuildingBlockLoggingDiagnostics("Custom.Diagnostics");
        Assert.Empty(LoggingEndpoints(disabled));

        await using var enabled = BuildDiagnosticsApplication(enabled: true);
        enabled.MapBuildingBlockLoggingDiagnostics("Custom.Diagnostics");
        var endpoints = LoggingEndpoints(enabled);

        Assert.Equal(2, endpoints.Length);
        Assert.All(endpoints, endpoint =>
        {
            var authorization = Assert.Single(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
            Assert.Equal("Custom.Diagnostics", authorization.Policy);
        });
    }

    private static WebApplication BuildDiagnosticsApplication(bool? enabled)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production"
        });
        if (enabled.HasValue)
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:Diagnostics:Enabled"] = enabled.Value.ToString()
            });
        }

        builder.Services.AddAuthorization();
        return builder.Build();
    }

    private static RouteEndpoint[] LoggingEndpoints(WebApplication application)
        => ((IEndpointRouteBuilder)application).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "/internal/logging",
                StringComparison.Ordinal) == true)
            .ToArray();

    private sealed class RedactionProbe
    {
        public string Name { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public RedactionProbe? Next { get; set; }
        public string Throwing => throw new InvalidOperationException("getter detail");
    }
}
