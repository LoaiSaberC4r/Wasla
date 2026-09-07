namespace BuildingBlock.Infrastructure.Options
{
    public sealed class EncryptionOptions
    {
        public const string SectionName = "Encryption";

        public string CurrentKeyId { get; set; } = "default";

        public Dictionary<string, string> Keys { get; set; } = new(StringComparer.Ordinal);

        public string KeyBase64 { get; set; } = string.Empty;

        public Dictionary<string, string> LegacyKeys { get; set; } = new(StringComparer.Ordinal);

        public string LegacyKeyBase64 { get; set; } = string.Empty;

        public Dictionary<string, string> GetConfiguredKeys()
        {
            if (Keys.Count > 0)
            {
                return new Dictionary<string, string>(Keys, StringComparer.Ordinal);
            }

            return string.IsNullOrWhiteSpace(KeyBase64)
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [CurrentKeyId] = KeyBase64
                };
        }

        public Dictionary<string, string> GetConfiguredLegacyKeys()
        {
            if (LegacyKeys.Count > 0)
            {
                return new Dictionary<string, string>(LegacyKeys, StringComparer.Ordinal);
            }

            return string.IsNullOrWhiteSpace(LegacyKeyBase64)
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["legacy"] = LegacyKeyBase64
                };
        }
    }

    internal static class EncryptionOptionsValidator
    {
        public const int KeySizeBytes = 32;
        public const string ValidationMessage =
            "Encryption options must define a current key id that references a configured 32-byte Base64 AES-GCM key.";

        public static bool IsValid(EncryptionOptions options)
            => Validate(options).Count == 0;

        public static IReadOnlyCollection<string> Validate(EncryptionOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var failures = new List<string>();
            var keys = options.GetConfiguredKeys();
            if (keys.Count == 0)
            {
                failures.Add("At least one encryption key is required.");
            }

            if (string.IsNullOrWhiteSpace(options.CurrentKeyId))
            {
                failures.Add("CurrentKeyId is required.");
            }
            else if (keys.Count > 0 && !keys.ContainsKey(options.CurrentKeyId))
            {
                failures.Add("CurrentKeyId must reference a configured key.");
            }

            foreach (var pair in keys)
            {
                ValidateKey(pair.Key, pair.Value, failures);
            }

            foreach (var pair in options.GetConfiguredLegacyKeys())
            {
                ValidateKey(pair.Key, pair.Value, failures);
            }

            return failures;
        }

        public static byte[] DecodeKey(string keyId, string keyBase64)
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new InvalidOperationException("Encryption key ids cannot be empty.");
            }

            byte[] key;
            try
            {
                key = Convert.FromBase64String(keyBase64);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException($"Encryption key '{keyId}' is not valid Base64.", exception);
            }

            if (key.Length != KeySizeBytes)
            {
                throw new InvalidOperationException(
                    $"Encryption key '{keyId}' must be {KeySizeBytes} bytes after Base64 decoding.");
            }

            return key;
        }

        private static void ValidateKey(string keyId, string keyBase64, List<string> failures)
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                failures.Add("Encryption key ids cannot be empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(keyBase64))
            {
                failures.Add($"Encryption key '{keyId}' is required.");
                return;
            }

            try
            {
                _ = DecodeKey(keyId, keyBase64);
            }
            catch (InvalidOperationException exception)
            {
                failures.Add(exception.Message);
            }
        }
    }
}
