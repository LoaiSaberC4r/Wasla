using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Infrastructure.Options;
using BuildingBlock.Infrastructure.Service;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace BuildingBlock.Tests;

public sealed class EncryptionServiceTests
{
    [Fact]
    public void Round_trip_encryption_uses_new_magic_header_format()
    {
        var service = CreateService("main", ("main", Key(1)));

        var encrypted = service.Encrypt("hello");
        var payload = Convert.FromBase64String(encrypted);
        var result = service.DecryptDetailed(encrypted);

        Assert.Equal("hello", result.Plaintext);
        Assert.False(result.ReEncryptionRequired);
        Assert.Equal("main", result.KeyId);
        Assert.Equal("BBENC", Encoding.ASCII.GetString(payload, 0, 5));
        Assert.Equal("main", ReadKeyId(payload));
    }

    [Fact]
    public void Same_plaintext_encrypts_to_different_ciphertext()
    {
        var service = CreateService("main", ("main", Key(2)));

        Assert.NotEqual(service.Encrypt("same"), service.Encrypt("same"));
    }

    [Theory]
    [InlineData(TamperTarget.Header)]
    [InlineData(TamperTarget.Ciphertext)]
    [InlineData(TamperTarget.Tag)]
    [InlineData(TamperTarget.Nonce)]
    public void Tampering_payload_fails_authentication(TamperTarget target)
    {
        var key = Key(3);
        var service = CreateService("main", ("main", key), ("copy", key));
        var payload = Convert.FromBase64String(service.Encrypt("secret"));

        if (target == TamperTarget.Header)
        {
            Encoding.UTF8.GetBytes("copy").CopyTo(payload, 7);
        }
        else if (target == TamperTarget.Nonce)
        {
            payload[11] ^= 0x01;
        }
        else if (target == TamperTarget.Tag)
        {
            payload[23] ^= 0x01;
        }
        else
        {
            payload[^1] ^= 0x01;
        }

        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(Convert.ToBase64String(payload)));
    }

    [Fact]
    public void Unknown_version_key_truncated_and_invalid_base64_fail_safely()
    {
        var service = CreateService("main", ("main", Key(4)));
        var payload = Convert.FromBase64String(service.Encrypt("secret"));

        var unknownVersion = payload.ToArray();
        unknownVersion[5] = 99;
        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(Convert.ToBase64String(unknownVersion)));

        var unknownKey = payload.ToArray();
        Encoding.UTF8.GetBytes("none").CopyTo(unknownKey, 7);
        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(Convert.ToBase64String(unknownKey)));

        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(Convert.ToBase64String(payload[..^5])));
        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt("not-base64"));
    }

    [Fact]
    public void Rotation_decrypts_old_key_payload_and_marks_reencryption_required()
    {
        var oldService = CreateService("old", ("old", Key(5)), ("new", Key(6)));
        var payload = oldService.Encrypt("rotate");

        var newService = CreateService("new", ("old", Key(5)), ("new", Key(6)));
        var result = newService.DecryptDetailed(payload);
        var newPayload = Convert.FromBase64String(newService.Encrypt("rotate"));

        Assert.Equal("rotate", result.Plaintext);
        Assert.True(result.ReEncryptionRequired);
        Assert.Equal("old", result.KeyId);
        Assert.Equal("new", ReadKeyId(newPayload));
        Assert.False(newService.DecryptDetailed(Convert.ToBase64String(newPayload)).ReEncryptionRequired);
    }

    [Fact]
    public void Legacy_payload_decrypts_with_configured_legacy_key_even_when_first_nonce_byte_is_one()
    {
        var key = Decode(Key(7));
        var legacyPayload = EncryptLegacy("legacy", key, firstNonceByte: 1);
        var service = CreateService(
            "main",
            new[] { ("main", Key(8)) },
            new[] { ("legacy", Convert.ToBase64String(key)) });

        var result = service.DecryptDetailed(legacyPayload);

        Assert.Equal("legacy", result.Plaintext);
        Assert.True(result.ReEncryptionRequired);
        Assert.Equal("legacy", result.KeyId);
    }

    [Fact]
    public void Legacy_payload_fails_when_no_configured_legacy_key_works()
    {
        var payload = EncryptLegacy("legacy", Decode(Key(9)), firstNonceByte: 1);
        var service = CreateService("main", ("main", Key(10)));

        Assert.Throws<CryptographicException>(() => service.Decrypt(payload));
    }

    private static EncryptionService CreateService(string currentKeyId, params (string Id, string Key)[] keys)
        => CreateService(currentKeyId, keys, Array.Empty<(string Id, string Key)>());

    private static EncryptionService CreateService(
        string currentKeyId,
        (string Id, string Key)[] keys,
        (string Id, string Key)[] legacyKeys)
        => new(Options.Create(new EncryptionOptions
        {
            CurrentKeyId = currentKeyId,
            Keys = keys.ToDictionary(pair => pair.Id, pair => pair.Key, StringComparer.Ordinal),
            LegacyKeys = legacyKeys.ToDictionary(pair => pair.Id, pair => pair.Key, StringComparer.Ordinal)
        }));

    private static string Key(byte seed)
        => Convert.ToBase64String(Enumerable.Range(0, 32).Select(offset => (byte)(seed + offset)).ToArray());

    private static byte[] Decode(string keyBase64)
        => Convert.FromBase64String(keyBase64);

    private static string ReadKeyId(byte[] payload)
    {
        var keyIdLength = payload[6];
        return Encoding.UTF8.GetString(payload, 7, keyIdLength);
    }

    private static string EncryptLegacy(string plaintext, byte[] key, byte firstNonceByte)
    {
        var nonce = Enumerable.Range(0, 12).Select(offset => (byte)(offset + 1)).ToArray();
        nonce[0] = firstNonceByte;
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        return Convert.ToBase64String(nonce.Concat(tag).Concat(ciphertext).ToArray());
    }

    public enum TamperTarget
    {
        Header,
        Ciphertext,
        Tag,
        Nonce
    }
}
