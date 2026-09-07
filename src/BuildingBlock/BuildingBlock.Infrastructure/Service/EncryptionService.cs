using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Infrastructure.Options;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace BuildingBlock.Infrastructure.Service
{
    internal sealed class EncryptionService : IEncryptionService
    {
        private const byte PayloadVersion = 1;
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private static ReadOnlySpan<byte> MagicHeader => "BBENC"u8;

        private readonly Dictionary<string, byte[]> _keys;
        private readonly Dictionary<string, byte[]> _legacyKeys;
        private readonly string _currentKeyId;

        public EncryptionService(IOptions<EncryptionOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var failures = EncryptionOptionsValidator.Validate(options.Value);
            if (failures.Count > 0)
            {
                throw new OptionsValidationException(
                    nameof(EncryptionOptions),
                    typeof(EncryptionOptions),
                    failures);
            }

            var configuredKeys = options.Value.GetConfiguredKeys();
            _currentKeyId = options.Value.CurrentKeyId;
            _keys = configuredKeys.ToDictionary(
                pair => pair.Key,
                pair => EncryptionOptionsValidator.DecodeKey(pair.Key, pair.Value),
                StringComparer.Ordinal);
            _legacyKeys = options.Value.GetConfiguredLegacyKeys().ToDictionary(
                pair => pair.Key,
                pair => EncryptionOptionsValidator.DecodeKey(pair.Key, pair.Value),
                StringComparer.Ordinal);
        }

        public string Encrypt(string plaintext)
        {
            ArgumentNullException.ThrowIfNull(plaintext);

            var keyIdBytes = Encoding.UTF8.GetBytes(_currentKeyId);
            if (keyIdBytes.Length is 0 or > byte.MaxValue)
            {
                throw new InvalidOperationException("Encryption key id must be between 1 and 255 UTF-8 bytes.");
            }

            var key = _keys[_currentKeyId];
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertext = new byte[plaintextBytes.Length];
            var tag = new byte[TagSize];
            var header = BuildHeader(keyIdBytes);

            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, header);

            var payload = new byte[header.Length + NonceSize + TagSize + ciphertext.Length];
            var offset = 0;
            Buffer.BlockCopy(header, 0, payload, offset, header.Length);
            offset += header.Length;
            Buffer.BlockCopy(nonce, 0, payload, offset, NonceSize);
            offset += NonceSize;
            Buffer.BlockCopy(tag, 0, payload, offset, TagSize);
            offset += TagSize;
            Buffer.BlockCopy(ciphertext, 0, payload, offset, ciphertext.Length);

            return Convert.ToBase64String(payload);
        }

        public string Decrypt(string ciphertextBase64)
            => DecryptDetailed(ciphertextBase64).Plaintext;

        public EncryptionDecryptionResult DecryptDetailed(string ciphertextBase64)
        {
            if (string.IsNullOrWhiteSpace(ciphertextBase64))
            {
                throw new ArgumentException("Ciphertext cannot be empty.", nameof(ciphertextBase64));
            }

            byte[] payload;
            try
            {
                payload = Convert.FromBase64String(ciphertextBase64);
            }
            catch (FormatException exception)
            {
                throw new CryptographicException("Ciphertext payload is not valid Base64.", exception);
            }

            return HasMagicHeader(payload)
                ? DecryptVersionedPayload(payload)
                : DecryptLegacyPayload(payload);
        }

        private EncryptionDecryptionResult DecryptVersionedPayload(byte[] payload)
        {
            var minimumHeaderLength = MagicHeader.Length + 2;
            if (payload.Length < minimumHeaderLength + NonceSize + TagSize)
            {
                throw new CryptographicException("Ciphertext payload is too short.");
            }

            var version = payload[MagicHeader.Length];
            if (version != PayloadVersion)
            {
                throw new CryptographicException("Ciphertext payload version is not supported.");
            }

            var keyIdLength = payload[MagicHeader.Length + 1];
            var headerLength = minimumHeaderLength + keyIdLength;
            if (keyIdLength == 0 || payload.Length < headerLength + NonceSize + TagSize)
            {
                throw new CryptographicException("Ciphertext payload has an invalid key id.");
            }

            var keyId = Encoding.UTF8.GetString(payload, minimumHeaderLength, keyIdLength);
            if (!_keys.TryGetValue(keyId, out var key))
            {
                throw new CryptographicException("Ciphertext key id is not configured.");
            }

            var offset = headerLength;
            var nonce = payload.AsSpan(offset, NonceSize).ToArray();
            offset += NonceSize;
            var tag = payload.AsSpan(offset, TagSize).ToArray();
            offset += TagSize;
            var ciphertext = payload.AsSpan(offset).ToArray();
            var plaintextBytes = new byte[ciphertext.Length];
            var associatedData = payload.AsSpan(0, headerLength).ToArray();

            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintextBytes, associatedData);

            return new EncryptionDecryptionResult(
                Encoding.UTF8.GetString(plaintextBytes),
                keyId,
                !string.Equals(keyId, _currentKeyId, StringComparison.Ordinal));
        }

        private EncryptionDecryptionResult DecryptLegacyPayload(byte[] payload)
        {
            if (payload.Length < NonceSize + TagSize)
            {
                throw new CryptographicException("Legacy ciphertext payload is invalid.");
            }

            if (_legacyKeys.Count == 0)
            {
                throw new CryptographicException("No legacy encryption keys are configured.");
            }

            var nonce = payload.AsSpan(0, NonceSize).ToArray();
            var tag = payload.AsSpan(NonceSize, TagSize).ToArray();
            var ciphertext = payload.AsSpan(NonceSize + TagSize).ToArray();

            foreach (var pair in _legacyKeys)
            {
                var plaintextBytes = new byte[ciphertext.Length];
                try
                {
                    using var aes = new AesGcm(pair.Value, TagSize);
                    aes.Decrypt(nonce, ciphertext, tag, plaintextBytes);
                    return new EncryptionDecryptionResult(
                        Encoding.UTF8.GetString(plaintextBytes),
                        pair.Key,
                        ReEncryptionRequired: true);
                }
                catch (CryptographicException)
                {
                    CryptographicOperations.ZeroMemory(plaintextBytes);
                }
            }

            throw new CryptographicException("Legacy ciphertext payload cannot be decrypted with configured keys.");
        }

        private static byte[] BuildHeader(byte[] keyIdBytes)
        {
            var header = new byte[MagicHeader.Length + 2 + keyIdBytes.Length];
            MagicHeader.CopyTo(header);
            header[MagicHeader.Length] = PayloadVersion;
            header[MagicHeader.Length + 1] = (byte)keyIdBytes.Length;
            Buffer.BlockCopy(keyIdBytes, 0, header, MagicHeader.Length + 2, keyIdBytes.Length);
            return header;
        }

        private static bool HasMagicHeader(byte[] payload)
            => payload.Length >= MagicHeader.Length && payload.AsSpan(0, MagicHeader.Length).SequenceEqual(MagicHeader);
    }
}
