using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BuildingBlock.Api.ProblemDetails
{
    internal sealed class ConfigureBuildingBlockApiBehaviorOptions : IConfigureOptions<ApiBehaviorOptions>
    {
        private static readonly Regex CollectionIndex = new(
            "\\[[^\\]]*\\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

        public void Configure(ApiBehaviorOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.InvalidModelStateResponseFactory = context =>
                new BuildingBlockProblemActionResult(CreateValidationErrors(context));
        }

        private static IReadOnlyCollection<Error> CreateValidationErrors(ActionContext context)
        {
            var errors = new List<Error>();
            foreach (var entry in context.ModelState)
            {
                var field = GetSafeFieldName(entry.Key);
                foreach (var modelError in entry.Value.Errors)
                {
                    var (code, message) = IsJsonError(entry.Key, modelError.Exception)
                        ? (ErrorCodes.Validation.InvalidJson, "Request body contains malformed JSON.")
                        : modelError.Exception is not null || entry.Value.RawValue is not null
                            ? (ErrorCodes.Validation.ModelBinding, "The request contains invalid input.")
                            : (ErrorCodes.Validation.Invalid, "The request contains invalid input.");

                    errors.Add(Error.Validation(
                        code,
                        message,
                        details: $"field:{field}",
                        source: field));
                }
            }

            var jsonErrors = errors
                .Where(error => error.Code == ErrorCodes.Validation.InvalidJson)
                .ToArray();

            return jsonErrors.Length > 0 ? jsonErrors : errors;
        }

        private static bool IsJsonError(string modelStateKey, Exception? exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (current is JsonException or InputFormatterException)
                {
                    return true;
                }
            }

            return modelStateKey == "$" || modelStateKey.StartsWith("$.", StringComparison.Ordinal);
        }

        private static string GetSafeFieldName(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key == "$")
            {
                return "request";
            }

            var normalized = CollectionIndex.Replace(key.Trim(), "[]");
            if (normalized.Length > 128 || normalized.Any(character =>
                    !char.IsLetterOrDigit(character) && character is not ('.' or '_' or '-' or '[' or ']')))
            {
                return "request";
            }

            return normalized;
        }
    }
}
