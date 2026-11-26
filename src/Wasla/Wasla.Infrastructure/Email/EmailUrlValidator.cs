namespace Wasla.Infrastructure.Email;

internal sealed class EmailUrlValidator
{
    private readonly HashSet<string> origins;

    internal EmailUrlValidator(string[] configuredOrigins)
    {
        origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in configuredOrigins)
            if (Parse(value) is { PathAndQuery: "/", Fragment.Length: 0 } uri)
                origins.Add(Origin(uri));
    }

    internal string? Validate(string? destination, bool legacyFooter = false)
    {
        var uri = Parse(destination);
        if (uri is null || !(legacyFooter && origins.Count == 0 || origins.Contains(Origin(uri)))) return null;
        return uri.AbsoluteUri;
    }

    internal string? ActivationLink(string? destination, string? tokenParameter, string token)
    {
        var validated = Validate(destination);
        if (validated is null || string.IsNullOrWhiteSpace(tokenParameter) ||
            tokenParameter.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-'))
            return null;

        var route = new Uri(validated);
        if (route.Query.Length != 0 || route.Fragment.Length != 0) return null;
        return new UriBuilder(route) { Query = $"{tokenParameter}={Uri.EscapeDataString(token)}" }.Uri.AbsoluteUri;
    }

    private static string Origin(Uri uri) => uri.GetLeftPart(UriPartial.Authority);

    private static Uri? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(c => char.IsControl(c) || c == '\\') ||
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 || string.IsNullOrEmpty(uri.Host) ||
            uri.Host.EndsWith(".example", StringComparison.OrdinalIgnoreCase))
            return null;
        return uri;
    }
}
