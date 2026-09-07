namespace BuildingBlock.Api.Options
{
    public sealed class RequestLoggingOptions
    {
        public bool IncludeClientIp { get; set; }

        public bool IncludeUserAgent { get; set; }

        public bool IncludeQueryString { get; set; }

        public bool IncludeRequestHeaders { get; set; }

        public bool IncludeResponseHeaders { get; set; }

        public int MaximumValueLength { get; set; } = 1024;

        public string[] ExcludedPaths { get; set; } =
        {
            "/health",
            "/health/live",
            "/health/ready"
        };

        public string[] AllowedRequestHeaders { get; set; } =
        {
            "Accept",
            "Accept-Language",
            "Content-Type",
            "TraceParent",
            "X-Correlation-ID"
        };

        public string[] AllowedResponseHeaders { get; set; } =
        {
            "Content-Type",
            "X-Correlation-ID"
        };
    }
}
