using Microsoft.Extensions.Options;

namespace Wasla.Api.Configuration;

public sealed class WaslaCorsOptions
{
    public const string SectionName = "Cors";

    public bool AllowAnyOrigin { get; set; }
    public bool AllowCredentials { get; set; }
    public string[] AllowedOrigins { get; set; } = [];
}

internal sealed class WaslaCorsOptionsValidator
    : IValidateOptions<WaslaCorsOptions>
{
    private readonly IHostEnvironment _environment;

    public WaslaCorsOptionsValidator(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    public ValidateOptionsResult Validate(
        string? name,
        WaslaCorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var configuredOrigins = options.AllowedOrigins ?? [];

        if (options.AllowAnyOrigin && options.AllowCredentials)
        {
            failures.Add(
                "CORS cannot enable both AllowAnyOrigin and AllowCredentials.");
        }

        if (options.AllowAnyOrigin && _environment.IsProduction())
        {
            failures.Add("CORS cannot enable AllowAnyOrigin in Production.");
        }

        if (options.AllowAnyOrigin && configuredOrigins.Length > 0)
        {
            failures.Add(
                "CORS AllowedOrigins must be empty when AllowAnyOrigin is enabled.");
        }

        if (!options.AllowAnyOrigin && configuredOrigins.Length == 0)
        {
            failures.Add(
                "CORS must specify at least one allowed origin when AllowAnyOrigin is disabled.");
        }

        var uniqueOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < configuredOrigins.Length; index++)
        {
            var configuredOrigin = configuredOrigins[index];
            if (string.IsNullOrWhiteSpace(configuredOrigin))
            {
                failures.Add(
                    $"CORS allowed origin at index {index} must not be empty.");
                continue;
            }

            var origin = configuredOrigin.Trim();
            if (!IsValidOrigin(origin))
            {
                failures.Add(
                    $"CORS allowed origin at index {index} must be an absolute HTTP or HTTPS origin without a path, query string, fragment, user information, or trailing slash.");
            }

            if (!uniqueOrigins.Add(origin))
            {
                failures.Add(
                    "CORS allowed origins must not contain duplicates.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidOrigin(string origin)
    {
        if (origin.EndsWith('/') ||
            !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (!string.Equals(
                 uri.Scheme,
                 Uri.UriSchemeHttp,
                 StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(
                 uri.Scheme,
                 Uri.UriSchemeHttps,
                 StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            uri.HostNameType == UriHostNameType.Unknown ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal))
        {
            return false;
        }

        var authorityStart =
            origin.IndexOf(Uri.SchemeDelimiter, StringComparison.Ordinal) +
            Uri.SchemeDelimiter.Length;
        return origin.IndexOfAny(['/', '\\', '?', '#'], authorityStart) < 0;
    }
}
