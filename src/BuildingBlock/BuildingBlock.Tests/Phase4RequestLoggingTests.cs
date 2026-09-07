using BuildingBlock.Api.Logging;
using BuildingBlock.Api.Options;
using Microsoft.AspNetCore.Http;

namespace BuildingBlock.Tests;

public sealed class Phase4RequestLoggingTests
{
    [Fact]
    public void Request_logging_privacy_defaults_are_disabled()
    {
        var options = new RequestLoggingOptions();

        Assert.False(options.IncludeClientIp);
        Assert.False(options.IncludeUserAgent);
        Assert.False(options.IncludeQueryString);
        Assert.False(options.IncludeRequestHeaders);
        Assert.False(options.IncludeResponseHeaders);
    }

    [Fact]
    public void Sensitive_query_values_are_redacted_without_logging_raw_query()
    {
        var sanitizer = new RequestLogSanitizer(
            new DefaultLogRedactionPolicy(),
            new RequestLoggingOptions { IncludeQueryString = true });

        var result = Assert.IsType<Dictionary<string, string[]>>(
            sanitizer.SanitizeQuery(new QueryString("?email=john@example.com&token=abc.def.ghi&safe=value")));

        Assert.Equal("***", Assert.Single(result["email"]));
        Assert.Equal("***", Assert.Single(result["token"]));
        Assert.Equal("value", Assert.Single(result["safe"]));
    }

    [Fact]
    public void Authorization_and_cookie_headers_are_never_logged_even_when_allowlisted()
    {
        var headers = new HeaderDictionary
        {
            ["Authorization"] = "Bearer abc.def.ghi",
            ["Cookie"] = "session=secret",
            ["Accept"] = "application/json"
        };
        var sanitizer = new RequestLogSanitizer(
            new DefaultLogRedactionPolicy(),
            new RequestLoggingOptions
            {
                IncludeRequestHeaders = true,
                AllowedRequestHeaders = new[] { "Authorization", "Cookie", "Accept" }
            });

        var result = Assert.IsType<Dictionary<string, string[]>>(
            sanitizer.SanitizeHeaders(headers, new[] { "Authorization", "Cookie", "Accept" }));

        Assert.DoesNotContain("Authorization", result.Keys);
        Assert.DoesNotContain("Cookie", result.Keys);
        Assert.Equal("application/json", Assert.Single(result["Accept"]));
    }

    [Fact]
    public void Excluded_paths_match_health_endpoints()
    {
        var sanitizer = new RequestLogSanitizer(
            new DefaultLogRedactionPolicy(),
            new RequestLoggingOptions());

        Assert.True(sanitizer.IsExcluded("/health"));
        Assert.True(sanitizer.IsExcluded("/health/ready"));
        Assert.False(sanitizer.IsExcluded("/api/records"));
    }
}
