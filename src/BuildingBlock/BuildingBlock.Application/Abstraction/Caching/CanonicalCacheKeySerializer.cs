using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace BuildingBlock.Application.Abstraction.Caching
{
    internal static class CanonicalCacheKeySerializer
    {
        internal const int MaximumDepth = 16;
        internal const int MaximumCollectionItems = 1024;
        internal const int MaximumDictionaryEntries = 512;
        internal const int MaximumProperties = 128;
        internal const int MaximumPayloadCharacters = 32 * 1024;

        private static readonly ConcurrentDictionary<Type, PropertyMetadata[]> PropertyCache = new();

        public static bool TrySerialize(object? value, out string payload, out string? skipReason)
        {
            try
            {
                var writer = new CanonicalWriter();
                writer.WriteValue(value, depth: 0);
                payload = writer.ToString();
                skipReason = null;
                return true;
            }
            catch (CanonicalizationException exception)
            {
                payload = string.Empty;
                skipReason = exception.Reason;
                return false;
            }
            catch (Exception exception) when (!IsProcessCritical(exception))
            {
                payload = string.Empty;
                skipReason = "cache-key-value-unreadable";
                return false;
            }
        }

        private static bool IsProcessCritical(Exception exception)
        {
            if (exception is OutOfMemoryException or StackOverflowException or AccessViolationException)
            {
                return true;
            }

            return exception.InnerException is not null && IsProcessCritical(exception.InnerException);
        }

        private static PropertyMetadata[] GetProperties(Type type)
            => PropertyCache.GetOrAdd(type, static cachedType =>
                cachedType
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(static property =>
                        property.CanRead &&
                        property.GetMethod?.IsStatic == false &&
                        property.GetIndexParameters().Length == 0 &&
                        property.GetCustomAttribute<JsonIgnoreAttribute>(inherit: true) is null &&
                        property.GetCustomAttribute<CacheKeyIgnoreAttribute>(inherit: true) is null)
                    .OrderBy(static property => property.Name, StringComparer.Ordinal)
                    .Select(static property => new PropertyMetadata(
                        property,
                        IsSensitivePropertyName(property.Name)))
                    .ToArray());

        private static bool IsSensitivePropertyName(string propertyName)
        {
            var normalized = propertyName
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal);

            return normalized.Equals("Password", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("Password", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("PasswordHash", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Secret", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("Secret", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Token", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("AccessToken", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("RefreshToken", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("ApiKey", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("ClientSecret", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("EncryptionKey", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class CanonicalWriter
        {
            private readonly StringBuilder _builder = new();
            private readonly HashSet<object> _activeReferences = new(ReferenceEqualityComparer.Instance);

            public void WriteValue(object? value, int depth)
            {
                if (depth > MaximumDepth)
                {
                    throw new CanonicalizationException("cache-key-depth-limit");
                }

                if (value is null)
                {
                    Append("n;");
                    return;
                }

                switch (value)
                {
                    case string text:
                        WriteText("s", text);
                        return;
                    case char character:
                        WriteText("c", character.ToString());
                        return;
                    case bool boolean:
                        Append(boolean ? "b:1;" : "b:0;");
                        return;
                    case byte number:
                        WriteInvariant("u8", number);
                        return;
                    case sbyte number:
                        WriteInvariant("i8", number);
                        return;
                    case short number:
                        WriteInvariant("i16", number);
                        return;
                    case ushort number:
                        WriteInvariant("u16", number);
                        return;
                    case int number:
                        WriteInvariant("i32", number);
                        return;
                    case uint number:
                        WriteInvariant("u32", number);
                        return;
                    case long number:
                        WriteInvariant("i64", number);
                        return;
                    case ulong number:
                        WriteInvariant("u64", number);
                        return;
                    case decimal number:
                        WriteText("dec", number.ToString(CultureInfo.InvariantCulture));
                        return;
                    case float number:
                        WriteText("f32", number.ToString("R", CultureInfo.InvariantCulture));
                        return;
                    case double number:
                        WriteText("f64", number.ToString("R", CultureInfo.InvariantCulture));
                        return;
                    case Guid guid:
                        WriteText("guid", guid.ToString("N"));
                        return;
                    case DateTime dateTime:
                        WriteText("dt", NormalizeDateTime(dateTime).ToString("O", CultureInfo.InvariantCulture));
                        return;
                    case DateTimeOffset dateTimeOffset:
                        WriteText("dto", dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                        return;
                    case DateOnly dateOnly:
                        WriteText("date", dateOnly.ToString("O", CultureInfo.InvariantCulture));
                        return;
                    case TimeOnly timeOnly:
                        WriteText("time", timeOnly.ToString("O", CultureInfo.InvariantCulture));
                        return;
                    case TimeSpan timeSpan:
                        WriteText("span", timeSpan.ToString("c", CultureInfo.InvariantCulture));
                        return;
                    case Uri uri:
                        WriteText("uri", uri.OriginalString);
                        return;
                    case Enum enumValue:
                        WriteEnum(enumValue);
                        return;
                    case IDictionary dictionary:
                        WriteDictionary(dictionary, depth);
                        return;
                    case IEnumerable enumerable:
                        if (IsDictionaryType(value.GetType()))
                        {
                            WriteGenericDictionary(enumerable, depth);
                        }
                        else
                        {
                            WriteEnumerable(enumerable, value.GetType(), depth);
                        }

                        return;
                    default:
                        WriteObject(value, depth);
                        return;
                }
            }

            public override string ToString() => _builder.ToString();

            private void WriteEnum(Enum value)
            {
                var enumType = value.GetType();
                var underlyingType = Enum.GetUnderlyingType(enumType);
                var underlyingValue = Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
                Append("e{");
                WriteText("t", TypeIdentity(enumType));
                WriteValue(underlyingValue, depth: 0);
                Append("};");
            }

            private void WriteDictionary(IDictionary dictionary, int depth)
            {
                if (dictionary.Count > MaximumDictionaryEntries)
                {
                    throw new CanonicalizationException("cache-key-collection-limit");
                }

                EnterReference(dictionary);
                try
                {
                    var entries = new List<(string Key, string Value)>(dictionary.Count);
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        entries.Add((SerializeNested(entry.Key, depth + 1), SerializeNested(entry.Value, depth + 1)));
                    }

                    WriteSortedDictionaryEntries(dictionary.GetType(), entries);
                }
                finally
                {
                    ExitReference(dictionary);
                }
            }

            private void WriteGenericDictionary(IEnumerable dictionary, int depth)
            {
                EnterReference(dictionary);
                try
                {
                    var entries = new List<(string Key, string Value)>();
                    foreach (var entry in dictionary)
                    {
                        if (entries.Count == MaximumDictionaryEntries)
                        {
                            throw new CanonicalizationException("cache-key-collection-limit");
                        }

                        if (entry is null)
                        {
                            throw new CanonicalizationException("cache-key-unsupported-value");
                        }

                        var entryType = entry.GetType();
                        var keyProperty = entryType.GetProperty("Key", BindingFlags.Instance | BindingFlags.Public);
                        var valueProperty = entryType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                        if (keyProperty is null || valueProperty is null)
                        {
                            throw new CanonicalizationException("cache-key-unsupported-value");
                        }

                        entries.Add((
                            SerializeNested(keyProperty.GetValue(entry), depth + 1),
                            SerializeNested(valueProperty.GetValue(entry), depth + 1)));
                    }

                    WriteSortedDictionaryEntries(dictionary.GetType(), entries);
                }
                finally
                {
                    ExitReference(dictionary);
                }
            }

            private void WriteSortedDictionaryEntries(
                Type dictionaryType,
                List<(string Key, string Value)> entries)
            {
                entries.Sort(static (left, right) =>
                {
                    var keyComparison = string.CompareOrdinal(left.Key, right.Key);
                    return keyComparison != 0
                        ? keyComparison
                        : string.CompareOrdinal(left.Value, right.Value);
                });

                Append("d{");
                WriteText("t", TypeIdentity(dictionaryType));
                foreach (var (key, value) in entries)
                {
                    WriteText("k", key);
                    WriteText("v", value);
                }

                Append("};");
            }

            private void WriteEnumerable(IEnumerable enumerable, Type type, int depth)
            {
                EnterReference(enumerable);
                try
                {
                    var items = new List<string>();
                    foreach (var item in enumerable)
                    {
                        if (items.Count == MaximumCollectionItems)
                        {
                            throw new CanonicalizationException("cache-key-collection-limit");
                        }

                        items.Add(SerializeNested(item, depth + 1));
                    }

                    var isSet = IsSetType(type);
                    if (isSet)
                    {
                        items.Sort(StringComparer.Ordinal);
                    }

                    Append(isSet ? "set{" : "seq{");
                    WriteText("t", TypeIdentity(type));
                    foreach (var item in items)
                    {
                        WriteText("i", item);
                    }

                    Append("};");
                }
                finally
                {
                    ExitReference(enumerable);
                }
            }

            private void WriteObject(object value, int depth)
            {
                var type = value.GetType();
                var properties = GetProperties(type);
                if (properties.Length == 0)
                {
                    throw new CanonicalizationException("cache-key-unsupported-value");
                }

                if (properties.Length > MaximumProperties)
                {
                    throw new CanonicalizationException("cache-key-property-limit");
                }

                EnterReference(value);
                try
                {
                    Append("o{");
                    WriteText("t", TypeIdentity(type));
                    foreach (var property in properties)
                    {
                        var propertyValue = property.Property.GetValue(value);
                        if (property.IsSensitive && propertyValue is not null)
                        {
                            throw new CanonicalizationException("sensitive-cache-input");
                        }

                        WriteText("p", property.Property.Name);
                        WriteValue(propertyValue, depth + 1);
                    }

                    Append("};");
                }
                finally
                {
                    ExitReference(value);
                }
            }

            private string SerializeNested(object? value, int depth)
            {
                var start = _builder.Length;
                WriteValue(value, depth);
                var serialized = _builder.ToString(start, _builder.Length - start);
                _builder.Length = start;
                return serialized;
            }

            private void EnterReference(object value)
            {
                if (value.GetType().IsValueType)
                {
                    return;
                }

                if (!_activeReferences.Add(value))
                {
                    throw new CanonicalizationException("cache-key-cycle-detected");
                }
            }

            private void ExitReference(object value)
            {
                if (!value.GetType().IsValueType)
                {
                    _activeReferences.Remove(value);
                }
            }

            private void WriteInvariant<T>(string tag, T value)
                where T : IFormattable
                => WriteText(tag, value.ToString(null, CultureInfo.InvariantCulture));

            private void WriteText(string tag, string value)
            {
                Append(tag);
                Append(":");
                Append(value.Length.ToString(CultureInfo.InvariantCulture));
                Append(":");
                Append(value);
                Append(";");
            }

            private void Append(string value)
            {
                if (_builder.Length > MaximumPayloadCharacters - value.Length)
                {
                    throw new CanonicalizationException("cache-key-payload-limit");
                }

                _builder.Append(value);
            }

            private static DateTime NormalizeDateTime(DateTime value)
                => value.Kind switch
                {
                    DateTimeKind.Utc => value,
                    DateTimeKind.Local => value.ToUniversalTime(),
                    _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
                };

            private static bool IsSetType(Type type)
                => type.GetInterfaces().Any(static candidate =>
                    candidate.IsGenericType &&
                    (candidate.GetGenericTypeDefinition() == typeof(ISet<>) ||
                     candidate.GetGenericTypeDefinition() == typeof(IReadOnlySet<>)));

            private static bool IsDictionaryType(Type type)
                => type.GetInterfaces().Any(static candidate =>
                    candidate.IsGenericType &&
                    (candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>) ||
                     candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

            private static string TypeIdentity(Type type)
                => $"{type.Assembly.GetName().Name}:{type.FullName ?? type.Name}";
        }

        private sealed class CanonicalizationException : Exception
        {
            public CanonicalizationException(string reason)
                : base(reason)
            {
                Reason = reason;
            }

            public string Reason { get; }
        }

        private sealed record PropertyMetadata(PropertyInfo Property, bool IsSensitive);
    }
}
