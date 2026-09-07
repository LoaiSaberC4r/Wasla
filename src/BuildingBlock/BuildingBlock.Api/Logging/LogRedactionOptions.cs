using Microsoft.Extensions.Options;
using System.Collections;
using System.Text.RegularExpressions;

namespace BuildingBlock.Api.Logging
{
    public sealed class LogRedactionOptions
    {
        public string Mask { get; set; } = "***";

        public int MaximumStringLength { get; set; } = 2048;

        public int MaximumDepth { get; set; } = 8;

        public string[] SensitivePropertyNames { get; set; } =
        {
            "Password",
            "PasswordHash",
            "Secret",
            "Token",
            "AccessToken",
            "RefreshToken",
            "Authorization",
            "ApiKey",
            "ClientSecret",
            "Email",
            "Phone",
            "Mobile"
        };

        public bool RedactEmailAddresses { get; set; } = true;

        public bool RedactPhoneNumbers { get; set; } = true;

        public bool RedactBearerTokens { get; set; } = true;
    }

    public sealed record LogRedactionResult(object? Value, bool Changed);

    public interface ILogRedactionPolicy
    {
        LogRedactionResult Redact(string propertyName, object? value);
    }

    internal sealed class DefaultLogRedactionPolicy : ILogRedactionPolicy
    {
        private const string TruncationSuffix = "...[truncated]";
        private static readonly string[] RedactionMaskRequiredFailure = { "Redaction mask is required." };
        private static readonly string[] MaximumStringLengthFailure = { "Maximum redacted string length must be greater than the truncation suffix." };
        private static readonly string[] MaximumDepthFailure = { "Maximum redaction depth must be between 1 and 64." };
        private static readonly string[] SensitivePropertyNamesFailure = { "At least one non-empty sensitive property name is required." };

        private static readonly Regex Email = new(
            @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(200));

        private static readonly Regex Phone = new(
            @"\+?\d[\d\s\-]{7,}\d",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(200));

        private static readonly Regex BearerToken = new(
            @"\bBearer\s+[A-Za-z0-9\-._~+/]+=*",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(200));

        private readonly LogRedactionOptions _options;
        private readonly HashSet<string> _sensitiveNames;

        public DefaultLogRedactionPolicy(IOptions<LogRedactionOptions> options)
            : this(options.Value)
        {
        }

        public DefaultLogRedactionPolicy(LogRedactionOptions? options = null)
        {
            _options = options ?? new LogRedactionOptions();
            Validate(_options);
            _sensitiveNames = _options.SensitivePropertyNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public LogRedactionResult Redact(string propertyName, object? value)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var redacted = RedactCore(propertyName, value, depth: 0, visited);
            return new LogRedactionResult(redacted, !Equals(value, redacted));
        }

        public static bool IsValid(LogRedactionOptions options)
        {
            try
            {
                Validate(options);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal bool IsSensitiveName(string? propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            var normalized = propertyName.Trim();
            if (_sensitiveNames.Contains(normalized))
            {
                return true;
            }

            var finalSegment = normalized
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault();

            return !string.IsNullOrWhiteSpace(finalSegment) &&
                   _sensitiveNames.Contains(finalSegment);
        }

        internal string RedactString(string value)
        {
            try
            {
                var redacted = value;
                if (_options.RedactBearerTokens)
                {
                    redacted = BearerToken.Replace(redacted, "Bearer " + _options.Mask);
                }

                if (_options.RedactEmailAddresses)
                {
                    redacted = Email.Replace(redacted, _options.Mask);
                }

                if (_options.RedactPhoneNumbers)
                {
                    redacted = Phone.Replace(redacted, _options.Mask);
                }

                return Truncate(redacted);
            }
            catch (RegexMatchTimeoutException)
            {
                return _options.Mask;
            }
        }

        internal object? RedactSensitiveValue(object? value)
            => value is null ? null : _options.Mask;

        private object? RedactCore(
            string propertyName,
            object? value,
            int depth,
            HashSet<object> visited)
        {
            if (value is null)
            {
                return null;
            }

            if (IsSensitiveName(propertyName))
            {
                return _options.Mask;
            }

            if (value is string text)
            {
                return RedactString(text);
            }

            if (IsSimpleScalar(value))
            {
                return value;
            }

            if (depth >= _options.MaximumDepth)
            {
                return _options.Mask;
            }

            if (!visited.Add(value))
            {
                return _options.Mask;
            }

            if (value is IDictionary dictionary)
            {
                var result = new Dictionary<object, object?>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = entry.Key is string keyText
                        ? RedactString(keyText)
                        : entry.Key;
                    var childName = entry.Key?.ToString() ?? propertyName;
                    result[key ?? _options.Mask] = IsSensitiveName(childName)
                        ? _options.Mask
                        : RedactCore(Join(propertyName, childName), entry.Value, depth + 1, visited);
                }

                return result;
            }

            if (value is IEnumerable enumerable)
            {
                var result = new List<object?>();
                foreach (var item in enumerable)
                {
                    result.Add(RedactCore(propertyName, item, depth + 1, visited));
                }

                return result;
            }

            var properties = value.GetType()
                .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .Where(property => property.GetIndexParameters().Length == 0)
                .ToArray();
            if (properties.Length == 0)
            {
                return value;
            }

            var shaped = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in properties)
            {
                var childName = Join(propertyName, property.Name);
                object? childValue;
                try
                {
                    childValue = property.GetValue(value);
                }
                catch
                {
                    shaped[property.Name] = _options.Mask;
                    continue;
                }

                shaped[property.Name] = RedactCore(childName, childValue, depth + 1, visited);
            }

            return shaped;
        }

        private string Truncate(string value)
        {
            if (value.Length <= _options.MaximumStringLength)
            {
                return value;
            }

            var prefixLength = Math.Max(0, _options.MaximumStringLength - TruncationSuffix.Length);
            return value[..prefixLength] + TruncationSuffix;
        }

        private static string Join(string parent, string child)
            => string.IsNullOrWhiteSpace(parent) ? child : $"{parent}.{child}";

        private static bool IsSimpleScalar(object value)
            => value.GetType().IsPrimitive ||
               value is decimal or DateTime or DateTimeOffset or TimeSpan or Guid or Uri or Enum;

        private static void Validate(LogRedactionOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (string.IsNullOrEmpty(options.Mask))
            {
                throw new OptionsValidationException(
                    nameof(LogRedactionOptions),
                    typeof(LogRedactionOptions),
                    RedactionMaskRequiredFailure);
            }

            if (options.MaximumStringLength <= TruncationSuffix.Length)
            {
                throw new OptionsValidationException(
                    nameof(LogRedactionOptions),
                    typeof(LogRedactionOptions),
                    MaximumStringLengthFailure);
            }

            if (options.MaximumDepth <= 0 || options.MaximumDepth > 64)
            {
                throw new OptionsValidationException(
                    nameof(LogRedactionOptions),
                    typeof(LogRedactionOptions),
                    MaximumDepthFailure);
            }

            if (options.SensitivePropertyNames.Length == 0 ||
                options.SensitivePropertyNames.Any(string.IsNullOrWhiteSpace))
            {
                throw new OptionsValidationException(
                    nameof(LogRedactionOptions),
                    typeof(LogRedactionOptions),
                    SensitivePropertyNamesFailure);
            }
        }
    }
}
