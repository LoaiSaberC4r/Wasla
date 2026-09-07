namespace BuildingBlock.Domain.Results
{
    public static class ErrorCodes
    {
        public static class Common
        {
            public const string BadRequest = "Common.BadRequest";
            public const string Validation = "Common.Validation";
            public const string NotFound = "Common.NotFound";
            public const string Conflict = "Common.Conflict";
            public const string Unauthorized = "Common.Unauthorized";
            public const string Forbidden = "Common.Forbidden";
            public const string RateLimited = "Common.RateLimited";
            public const string Infrastructure = "Common.Infrastructure";
            public const string Unknown = "Common.Unknown";
            public const string MethodNotAllowed = "Common.MethodNotAllowed";
            public const string UnsupportedMediaType = "Common.UnsupportedMediaType";
        }

        public static class Validation
        {
            public const string Required = "Validation.Required";
            public const string Invalid = "Validation.Invalid";
            public const string InvalidJson = "Validation.InvalidJson";
            public const string ModelBinding = "Validation.ModelBinding";
        }

        public static class Persistence
        {
            public const string Concurrency = "Persistence.Concurrency";
            public const string UniqueConstraint = "Persistence.UniqueConstraint";
            public const string Deadlock = "Persistence.Deadlock";
            public const string Timeout = "Persistence.Timeout";
            public const string Unavailable = "Persistence.Unavailable";
        }
    }
}
