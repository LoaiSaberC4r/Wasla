using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Infrastructure.Service;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace BuildingBlock.Tests;

public sealed class TokenReaderTests
{
    [Fact]
    public void Role_claims_with_spaces_are_not_split()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.Role, "Technical Admin"));

        var token = CreateReader().ReadFromPrincipal(principal);

        Assert.Equal(new[] { "Technical Admin" }, token.Roles);
    }

    [Fact]
    public void Comma_separated_role_claims_are_split()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.Role, "Admin,Editor"));

        var token = CreateReader().ReadFromPrincipal(principal);

        Assert.Equal(new[] { "Admin", "Editor" }, token.Roles);
    }

    [Theory]
    [InlineData("read write")]
    [InlineData("read,write")]
    public void Permission_claims_split_on_spaces_and_commas(string permissionValue)
    {
        var principal = CreatePrincipal(new Claim("permission", permissionValue));

        var token = CreateReader().ReadFromPrincipal(principal);

        Assert.Equal(new[] { "read", "write" }, token.Permissions);
    }

    [Fact]
    public void Mixed_permission_separators_are_split()
    {
        var principal = CreatePrincipal(new Claim("permission", "read write,delete"));

        var token = CreateReader().ReadFromPrincipal(principal);

        Assert.Equal(new[] { "read", "write", "delete" }, token.Permissions);
    }

    [Fact]
    public void Duplicate_roles_are_distinct_case_insensitively()
    {
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("role", "admin"),
            new Claim("roles", "Manager,Admin"));

        var token = CreateReader().ReadFromPrincipal(principal);

        Assert.Equal(new[] { "Admin", "Manager" }, token.Roles);
    }

    [Fact]
    public void Valid_epoch_claims_are_parsed()
    {
        var principal = CreatePrincipal(
            new Claim("iat", "1700000000"),
            new Claim("exp", "1700003600"));

        var token = CreateReader().ReadFromPrincipal(principal);

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime, token.IssuedAtUtc);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700003600).UtcDateTime, token.ExpiresAtUtc);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("9223372036854775807")]
    public void Invalid_epoch_claims_return_null(string epoch)
    {
        var principal = CreatePrincipal(new Claim("iat", epoch), new Claim("exp", epoch));

        var token = CreateReader().ReadFromPrincipal(principal);

        Assert.Null(token.IssuedAtUtc);
        Assert.Null(token.ExpiresAtUtc);
    }

    [Fact]
    public void Custom_claim_types_are_supported()
    {
        var principal = CreatePrincipal(
            new Claim("user_id", Guid.NewGuid().ToString()),
            new Claim("perm", "read"));
        var reader = CreateReader(new CurrentUserClaimOptions
        {
            UserIdClaimType = "user_id",
            PermissionClaimType = "perm"
        });

        var token = reader.ReadFromPrincipal(principal);

        Assert.NotNull(token.UserId);
        Assert.Equal(new[] { "read" }, token.Permissions);
    }

    [Fact]
    public void Claim_type_matching_is_case_insensitive()
    {
        var principal = CreatePrincipal(new Claim("PERMISSION", "read"));

        var token = CreateReader().ReadFromPrincipal(principal);

        Assert.Equal(new[] { "read" }, token.Permissions);
    }

    [Fact]
    public void Oversized_claim_values_are_ignored()
    {
        var principal = CreatePrincipal(new Claim("permission", new string('x', 12)));
        var reader = CreateReader(new CurrentUserClaimOptions { MaximumClaimValueLength = 8 });

        var token = reader.ReadFromPrincipal(principal);

        Assert.Empty(token.Permissions);
    }

    [Fact]
    public void Exceeding_permission_count_returns_no_permissions()
    {
        var principal = CreatePrincipal(new Claim("permission", "one two three"));
        var reader = CreateReader(new CurrentUserClaimOptions { MaximumPermissions = 2 });

        var token = reader.ReadFromPrincipal(principal);

        Assert.Empty(token.Permissions);
    }

    private static TokenReader CreateReader()
        => new(Options.Create(new CurrentUserClaimOptions()));

    private static TokenReader CreateReader(CurrentUserClaimOptions options)
        => new(Options.Create(options));

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "Test"));
}
