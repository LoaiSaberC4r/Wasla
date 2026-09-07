using System.Globalization;

namespace BuildingBlock.Api.Options
{
    public sealed class SharedLocalizationOptions
    {
        public const string SectionName = "Localization";

        public string[] SupportedCultures { get; set; } = new[] { "ar", "en" };

        public string DefaultCulture { get; set; } = "en";

        public bool AllowQueryStringLang { get; set; } = true;

        public void Validate()
        {
            var failures = SharedLocalizationOptionsValidator.Validate(this);
            if (failures.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" ", failures));
            }
        }
    }

    internal static class SharedLocalizationOptionsValidator
    {
        public const string ValidationMessage =
            "Localization options must define valid, distinct supported cultures and include the default culture.";

        public static bool IsValid(SharedLocalizationOptions options)
            => Validate(options).Count == 0;

        public static IReadOnlyCollection<string> Validate(SharedLocalizationOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var failures = new List<string>();
            if (options.SupportedCultures.Length == 0)
            {
                failures.Add("At least one supported culture is required.");
                return failures;
            }

            var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var culture in options.SupportedCultures)
            {
                if (string.IsNullOrWhiteSpace(culture))
                {
                    failures.Add("Supported cultures cannot contain empty values.");
                    continue;
                }

                if (!distinct.Add(culture))
                {
                    failures.Add($"Duplicate supported culture '{culture}' is not allowed.");
                    continue;
                }

                if (!IsKnownCulture(culture))
                {
                    failures.Add($"Supported culture '{culture}' is invalid.");
                }
            }

            if (string.IsNullOrWhiteSpace(options.DefaultCulture))
            {
                failures.Add("Default culture is required.");
            }
            else if (!IsKnownCulture(options.DefaultCulture))
            {
                failures.Add($"Default culture '{options.DefaultCulture}' is invalid.");
            }
            else if (!options.SupportedCultures.Contains(options.DefaultCulture, StringComparer.OrdinalIgnoreCase))
            {
                failures.Add("Default culture must be listed in SupportedCultures.");
            }

            return failures;
        }

        private static bool IsKnownCulture(string culture)
        {
            try
            {
                _ = CultureInfo.GetCultureInfo(culture);
                return true;
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }
    }
}
