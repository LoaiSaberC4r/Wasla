using BuildingBlock.Api.Security;
using BuildingBlock.Application.Abstraction.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace BuildingBlock.Tests;

public sealed class HttpCurrentUserTests
{
    [Fact]
    public void No_http_context_returns_anonymous_defaults()
    {
        using var provider = CreateProvider(out _);
        using var scope = provider.CreateScope();

        var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.UserId);
        Assert.Null(user.UserName);
        Assert.Null(user.Email);
        Assert.Empty(user.Roles);
        Assert.Empty(user.Permissions);
    }

    [Fact]
    public void Unauthenticated_principal_with_claims_does_not_expose_claim_data()
    {
        using var provider = CreateProvider(out var accessor);
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, "hidden")
            }))
        };

        using var scope = provider.CreateScope();
        var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.UserId);
        Assert.Null(user.UserName);
        Assert.Null(user.GetClaimValue(ClaimTypes.Name));
        Assert.Empty(user.GetClaimValues(ClaimTypes.Name));
    }

    [Fact]
    public void Authenticated_principal_exposes_configured_claims()
    {
        var userId = Guid.NewGuid();
        using var provider = CreateProvider(out var accessor);
        accessor.HttpContext = new DefaultHttpContext
        {
            User = AuthenticatedPrincipal(
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "alice"),
                new Claim(ClaimTypes.Email, "alice@example.test"),
                new Claim(ClaimTypes.Role, "Admin,Manager"),
                new Claim("permission", "read write,delete"))
        };

        using var scope = provider.CreateScope();
        var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.True(user.IsAuthenticated);
        Assert.Equal(userId, user.UserId);
        Assert.Equal("alice", user.UserName);
        Assert.Equal("alice@example.test", user.Email);
        Assert.Equal(new[] { "Admin", "Manager" }, user.Roles);
        Assert.Equal(new[] { "read", "write", "delete" }, user.Permissions);
    }

    [Fact]
    public void Invalid_guid_user_id_returns_null()
    {
        using var provider = CreateProvider(out var accessor);
        accessor.HttpContext = new DefaultHttpContext
        {
            User = AuthenticatedPrincipal(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"))
        };

        using var scope = provider.CreateScope();
        var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.Null(user.UserId);
    }

    [Fact]
    public void Claims_are_parsed_once_per_scoped_instance()
    {
        using var provider = CreateProvider(out var accessor);
        accessor.HttpContext = new DefaultHttpContext
        {
            User = AuthenticatedPrincipal(
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, "cached"))
        };

        using var scope = provider.CreateScope();
        var reader = (CountingTokenReader)scope.ServiceProvider.GetRequiredService<ITokenReader>();
        var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        _ = user.UserId;
        _ = user.UserName;
        _ = user.Email;
        _ = user.Roles;
        _ = user.Permissions;

        Assert.Equal(1, reader.ReadCount);
    }

    [Fact]
    public void Separate_scopes_do_not_share_cached_user_state()
    {
        using var provider = CreateProvider(out var accessor);

        accessor.HttpContext = new DefaultHttpContext { User = AuthenticatedPrincipal(new Claim(ClaimTypes.Name, "first")) };
        using var firstScope = provider.CreateScope();
        Assert.Equal("first", firstScope.ServiceProvider.GetRequiredService<ICurrentUser>().UserName);

        accessor.HttpContext = new DefaultHttpContext { User = AuthenticatedPrincipal(new Claim(ClaimTypes.Name, "second")) };
        using var secondScope = provider.CreateScope();
        Assert.Equal("second", secondScope.ServiceProvider.GetRequiredService<ICurrentUser>().UserName);
    }

    [Fact]
    public void Roles_and_permissions_are_immutable_to_callers()
    {
        using var provider = CreateProvider(out var accessor);
        accessor.HttpContext = new DefaultHttpContext
        {
            User = AuthenticatedPrincipal(
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("permission", "read"))
        };

        using var scope = provider.CreateScope();
        var user = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        Assert.False(user.Roles is string[]);
        Assert.False(user.Permissions is string[]);
    }

    private static ServiceProvider CreateProvider(out IHttpContextAccessor accessor)
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddSingleton<ITokenReader, CountingTokenReader>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        var provider = services.BuildServiceProvider();
        accessor = provider.GetRequiredService<IHttpContextAccessor>();
        return provider;
    }

    private static ClaimsPrincipal AuthenticatedPrincipal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "Test"));

    private sealed class CountingTokenReader : ITokenReader
    {
        private readonly BuildingBlock.Infrastructure.Service.TokenReader _inner =
            new(Microsoft.Extensions.Options.Options.Create(new CurrentUserClaimOptions()));

        public int ReadCount { get; private set; }

        public TokenInfoDto ReadFromPrincipal(ClaimsPrincipal principal)
        {
            ReadCount++;
            return _inner.ReadFromPrincipal(principal);
        }

        public bool TryGetClaim(ClaimsPrincipal principal, string claimType, out string? value)
            => _inner.TryGetClaim(principal, claimType, out value);

        public (bool ok, T? value) TryGet<T>(ClaimsPrincipal principal, string claimType)
            => _inner.TryGet<T>(principal, claimType);
    }
}
