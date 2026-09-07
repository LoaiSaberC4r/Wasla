using BuildingBlock.Api.Security;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Infrastructure.Bootstrap;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlock.Tests;

public sealed class SecurityRegistrationTests
{
    [Fact]
    public void Token_reader_registration_is_independent()
    {
        var services = new ServiceCollection();

        services.AddBuildingBlockTokenReader();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ITokenReader>());
        Assert.Null(provider.GetService<IPasswordService>());
        Assert.Null(provider.GetService<IEncryptionService>());
        Assert.Null(provider.GetService<ICurrentUser>());
    }

    [Fact]
    public void Password_hashing_registration_is_independent()
    {
        var services = new ServiceCollection();

        services.AddBuildingBlockPasswordHashing();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IPasswordService>());
        Assert.Null(provider.GetService<ITokenReader>());
        Assert.Null(provider.GetService<IEncryptionService>());
    }

    [Fact]
    public void Current_user_registration_requires_explicit_token_reader_registration()
    {
        var services = new ServiceCollection();

        services.AddBuildingBlockTokenReader();
        services.AddBuildingBlockCurrentUser();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IHttpContextAccessor>());
        Assert.NotNull(provider.GetRequiredService<ITokenReader>());
        Assert.NotNull(provider.GetRequiredService<ICurrentUser>());
        Assert.Null(provider.GetService<IPasswordService>());
        Assert.Null(provider.GetService<IEncryptionService>());
    }

    [Fact]
    public void Encryption_registration_with_valid_key_resolves()
    {
        var services = new ServiceCollection();

        services.AddBuildingBlockEncryption(ConfigurationFor(
            ("Encryption:CurrentKeyId", "main"),
            ("Encryption:Keys:main", Key(1))));

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IEncryptionService>());
    }

    [Theory]
    [MemberData(nameof(InvalidEncryptionConfigurations))]
    public void Encryption_registration_fails_for_invalid_configuration((string Key, string? Value)[] values)
    {
        var services = new ServiceCollection();
        services.AddBuildingBlockEncryption(ConfigurationFor(values));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IEncryptionService>());
    }

    [Fact]
    public void Full_security_smoke_registration_resolves_selected_services()
    {
        var services = new ServiceCollection();
        var configuration = ConfigurationFor(
            ("Encryption:CurrentKeyId", "main"),
            ("Encryption:Keys:main", Key(2)));

        services.AddBuildingBlockTokenReader(configuration);
        services.AddBuildingBlockCurrentUser();
        services.AddBuildingBlockPasswordHashing(configuration);
        services.AddBuildingBlockEncryption(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ITokenReader>());
        Assert.NotNull(provider.GetRequiredService<ICurrentUser>());
        Assert.NotNull(provider.GetRequiredService<IPasswordService>());
        Assert.NotNull(provider.GetRequiredService<IEncryptionService>());
    }

    public static IEnumerable<object[]> InvalidEncryptionConfigurations()
    {
        yield return new object[] { Array.Empty<(string, string?)>() };
        yield return new object[]
        {
            new[]
            {
                ("Encryption:CurrentKeyId", (string?)"main"),
                ("Encryption:Keys:main", "not-base64")
            }
        };
        yield return new object[]
        {
            new[]
            {
                ("Encryption:CurrentKeyId", (string?)"main"),
                ("Encryption:Keys:main", Convert.ToBase64String(new byte[16]))
            }
        };
        yield return new object[]
        {
            new[]
            {
                ("Encryption:Keys:main", (string?)Key(3))
            }
        };
        yield return new object[]
        {
            new[]
            {
                ("Encryption:CurrentKeyId", (string?)"missing"),
                ("Encryption:Keys:main", Key(4))
            }
        };
    }

    private static IConfiguration ConfigurationFor(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

    private static string Key(byte seed)
        => Convert.ToBase64String(Enumerable.Range(0, 32).Select(offset => (byte)(seed + offset)).ToArray());
}
