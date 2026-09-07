using Serilog.Core;
using Serilog.Events;

namespace BuildingBlock.Api.Logging
{
    internal sealed class RedactionEnricher : ILogEventEnricher
    {
        private readonly DefaultLogRedactionPolicy _policy;

        public RedactionEnricher()
            : this(new DefaultLogRedactionPolicy())
        {
        }

        public RedactionEnricher(DefaultLogRedactionPolicy policy)
        {
            _policy = policy;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            foreach (var property in logEvent.Properties.ToArray())
            {
                var redacted = RedactValue(property.Key, property.Value, depth: 0);
                if (!Equals(redacted, property.Value))
                {
                    logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, redacted));
                }
            }
        }

        private LogEventPropertyValue RedactValue(string propertyName, LogEventPropertyValue value, int depth)
        {
            if (_policy.IsSensitiveName(propertyName))
            {
                return new ScalarValue(_policy.RedactSensitiveValue(string.Empty));
            }

            if (depth > 64)
            {
                return new ScalarValue(_policy.RedactSensitiveValue(string.Empty));
            }

            return value switch
            {
                ScalarValue { Value: string text } => new ScalarValue(_policy.RedactString(text)),
                StructureValue structure => new StructureValue(
                    structure.Properties
                        .Select(property => new LogEventProperty(
                            property.Name,
                            RedactValue(Join(propertyName, property.Name), property.Value, depth + 1)))
                        .ToArray(),
                    structure.TypeTag),
                SequenceValue sequence => new SequenceValue(
                    sequence.Elements
                        .Select(element => RedactValue(propertyName, element, depth + 1))
                        .ToArray()),
                DictionaryValue dictionary => new DictionaryValue(dictionary.Elements.ToDictionary(
                    pair => RedactDictionaryKey(pair.Key),
                    pair =>
                    {
                        var keyText = pair.Key.Value?.ToString() ?? propertyName;
                        return _policy.IsSensitiveName(keyText)
                            ? new ScalarValue(_policy.RedactSensitiveValue(string.Empty))
                            : RedactValue(Join(propertyName, keyText), pair.Value, depth + 1);
                    })),
                _ => value
            };
        }

        private ScalarValue RedactDictionaryKey(ScalarValue key)
            => key.Value is string text ? new ScalarValue(_policy.RedactString(text)) : key;

        private static string Join(string parent, string child)
            => string.IsNullOrWhiteSpace(parent) ? child : $"{parent}.{child}";
    }
}
