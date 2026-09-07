namespace BuildingBlock.Domain.Results
{
    public enum ErrorType
    {
        Validation,
        Domain,
        NotFound,
        Conflict,
        Infrastructure,
        Unauthorized,
        Security,
        RateLimit,
        Unknown
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "Error is an established public contract and renaming it would be a breaking change.")]
    public sealed record Error(
        string Code,
        string Message,
        ErrorType Type,
        string? Details = null,
        string? Source = null,
        string? CorrelationId = null,
        TimeSpan? RetryAfter = null)
    {
        public static Error Validation(string code, string message, string? details = null, string? source = null)
            => new(code, message, ErrorType.Validation, details, source);

        public static Error NotFound(string code, string message, string? details = null, string? source = null)
            => new(code, message, ErrorType.NotFound, details, source);
        public static Error Domain(string code, string message, string? details = null, string? source = null)
    => new(code, message, ErrorType.Domain, details, source);

        public static Error Conflict(string code, string message, string? details = null, string? source = null)
            => new(code, message, ErrorType.Conflict, details, source);

        public static Error Security(string code, string message, string? details = null, string? source = null)
            => new(code, message, ErrorType.Security, details, source);

        public static Error Unauthorized(string code, string message, string? details = null, string? source = null)
            => new(code, message, ErrorType.Unauthorized, details, source);

        public static Error Infra(string code, string message, string? details = null, string? source = null, TimeSpan? retryAfter = null)
            => new(code, message, ErrorType.Infrastructure, details, source, null, retryAfter);

        public static Error RateLimit(string code, string message, TimeSpan retryAfter, string? details = null, string? source = null)
            => new(code, message, ErrorType.RateLimit, details, source, null, retryAfter);

        public static Error Unknown(string code, string message, string? details = null, string? source = null)
            => new(code, message, ErrorType.Unknown, details, source);
    }
}
