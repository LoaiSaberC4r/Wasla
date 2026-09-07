using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace BuildingBlock.Application.Diagnostics
{
    public static class BuildingBlockDiagnostics
    {
        public const string ActivitySourceName = "BuildingBlock";
        public const string MeterName = "BuildingBlock";

        private static readonly string? Version =
            typeof(BuildingBlockDiagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            typeof(BuildingBlockDiagnostics).Assembly.GetName().Version?.ToString();

        public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);
        public static readonly Meter Meter = new(MeterName, Version);

        private static readonly Counter<long> RequestsTotal = Meter.CreateCounter<long>(
            "buildingblock.requests.total",
            unit: "{request}",
            description: "Total BuildingBlock CQRS requests processed.");

        private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
            "buildingblock.request.duration",
            unit: "ms",
            description: "BuildingBlock CQRS request duration in milliseconds.");

        private static readonly Counter<long> RequestFailures = Meter.CreateCounter<long>(
            "buildingblock.request.failures",
            unit: "{request}",
            description: "Total BuildingBlock CQRS requests that completed with failure.");

        private static readonly Counter<long> ValidationFailures = Meter.CreateCounter<long>(
            "buildingblock.validation.failures",
            unit: "{failure}",
            description: "Total validation failures produced by BuildingBlock validation behavior.");

        private static readonly Counter<long> CacheHits = Meter.CreateCounter<long>(
            "buildingblock.cache.hits",
            unit: "{operation}",
            description: "Total cache hits.");

        private static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>(
            "buildingblock.cache.misses",
            unit: "{operation}",
            description: "Total cache misses.");

        private static readonly Counter<long> CacheSets = Meter.CreateCounter<long>(
            "buildingblock.cache.sets",
            unit: "{operation}",
            description: "Total cache set operations.");

        private static readonly Counter<long> CacheInvalidations = Meter.CreateCounter<long>(
            "buildingblock.cache.invalidations",
            unit: "{operation}",
            description: "Total cache invalidation operations.");

        private static readonly Counter<long> CacheInvalidationFailures = Meter.CreateCounter<long>(
            "buildingblock.cache.invalidation.failures",
            unit: "{operation}",
            description: "Total failed cache invalidation operations.");

        private static readonly Counter<long> TransactionsStarted = Meter.CreateCounter<long>(
            "buildingblock.transactions.started",
            unit: "{operation}",
            description: "Total BuildingBlock transactions started.");

        private static readonly Counter<long> TransactionsCommitted = Meter.CreateCounter<long>(
            "buildingblock.transactions.committed",
            unit: "{operation}",
            description: "Total BuildingBlock transactions committed.");

        private static readonly Counter<long> TransactionsRolledBack = Meter.CreateCounter<long>(
            "buildingblock.transactions.rolled_back",
            unit: "{operation}",
            description: "Total BuildingBlock transactions rolled back.");

        private static readonly Counter<long> TransactionCommitFailures = Meter.CreateCounter<long>(
            "buildingblock.transactions.commit_failures",
            unit: "{operation}",
            description: "Total BuildingBlock transaction commit failures.");

        private static readonly Counter<long> TransactionRollbackFailures = Meter.CreateCounter<long>(
            "buildingblock.transactions.rollback_failures",
            unit: "{operation}",
            description: "Total BuildingBlock transaction rollback failures.");

        private static readonly Counter<long> DomainEventsDispatched = Meter.CreateCounter<long>(
            "buildingblock.domain_events.dispatched",
            unit: "{operation}",
            description: "Total domain events dispatched.");

        private static readonly Counter<long> DomainEventFailures = Meter.CreateCounter<long>(
            "buildingblock.domain_events.failures",
            unit: "{operation}",
            description: "Total domain event dispatch failures.");

        private static readonly Counter<long> EmailSent = Meter.CreateCounter<long>(
            "buildingblock.email.sent",
            unit: "{operation}",
            description: "Total emails sent by BuildingBlock email senders.");

        private static readonly Counter<long> EmailFailures = Meter.CreateCounter<long>(
            "buildingblock.email.failures",
            unit: "{operation}",
            description: "Total email send failures.");

        private static readonly Counter<long> MediaFilesSaved = Meter.CreateCounter<long>(
            "buildingblock.media.files.saved",
            unit: "{operation}",
            description: "Total media files saved.");

        private static readonly Counter<long> MediaFailures = Meter.CreateCounter<long>(
            "buildingblock.media.failures",
            unit: "{operation}",
            description: "Total media operation failures.");

        private static readonly Histogram<long> MediaFileSize = Meter.CreateHistogram<long>(
            "buildingblock.media.file.size",
            unit: "By",
            description: "Size in bytes of saved media files.");

        private static readonly Counter<long> QrCodeGenerated = Meter.CreateCounter<long>(
            "buildingblock.qrcode.generated",
            unit: "{operation}",
            description: "Total QR codes generated.");

        private static readonly Counter<long> QrCodeFailures = Meter.CreateCounter<long>(
            "buildingblock.qrcode.failures",
            unit: "{operation}",
            description: "Total QR code generation failures.");

        public static string GetRequestKind(Type requestType)
        {
            ArgumentNullException.ThrowIfNull(requestType);

            if (typeof(ICommand).IsAssignableFrom(requestType) ||
                requestType.GetInterfaces().Any(type =>
                    type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(ICommand<>)))
            {
                return "command";
            }

            if (requestType.GetInterfaces().Any(type =>
                    type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(IQuery<>)))
            {
                return "query";
            }

            return "request";
        }

        public static void RecordRequest(string requestKind, string resultStatus, double elapsedMilliseconds)
        {
            var tags = new TagList
            {
                { "request.kind", NormalizeRequestKind(requestKind) },
                { "result.status", NormalizeResultStatus(resultStatus) }
            };

            RequestsTotal.Add(1, tags);
            RequestDuration.Record(elapsedMilliseconds, tags);

            if (resultStatus == "failure")
            {
                RequestFailures.Add(1, tags);
            }
        }

        public static void RecordValidationFailures(string requestKind, long count)
        {
            if (count <= 0)
            {
                return;
            }

            ValidationFailures.Add(count, new TagList
            {
                { "request.kind", NormalizeRequestKind(requestKind) },
                { "error.type", ErrorType.Validation.ToString() }
            });
        }

        public static void RecordCacheHit()
            => CacheHits.Add(1, CacheTags("get"));

        public static void RecordCacheMiss()
            => CacheMisses.Add(1, CacheTags("get"));

        public static void RecordCacheSet()
            => CacheSets.Add(1, CacheTags("set"));

        public static void RecordCacheInvalidation()
            => CacheInvalidations.Add(1, CacheTags("invalidate"));

        public static void RecordCacheInvalidationFailure()
            => CacheInvalidationFailures.Add(1, CacheTags("invalidate"));

        public static void RecordTransactionStarted()
            => TransactionsStarted.Add(1, new TagList { { "transaction.result", "started" } });

        public static void RecordTransactionCommitted()
            => TransactionsCommitted.Add(1, new TagList { { "transaction.result", "committed" } });

        public static void RecordTransactionRolledBack()
            => TransactionsRolledBack.Add(1, new TagList { { "transaction.result", "rolled_back" } });

        public static void RecordTransactionCommitFailed()
            => TransactionCommitFailures.Add(1, new TagList { { "transaction.result", "commit_failed" } });

        public static void RecordTransactionRollbackFailed()
            => TransactionRollbackFailures.Add(1, new TagList { { "transaction.result", "rollback_failed" } });

        public static void RecordDomainEventDispatched()
            => DomainEventsDispatched.Add(1, new TagList { { "result.status", "success" } });

        public static void RecordDomainEventFailure(string errorType)
            => DomainEventFailures.Add(1, new TagList
            {
                { "result.status", "failure" },
                { "error.type", NormalizeErrorType(errorType) }
            });

        public static void RecordEmailSent()
            => EmailSent.Add(1, new TagList { { "email.result", "success" } });

        public static void RecordEmailFailure(string errorType)
            => EmailFailures.Add(1, new TagList
            {
                { "email.result", "failure" },
                { "error.type", NormalizeErrorType(errorType) }
            });

        public static void RecordMediaFileSaved(long bytes)
        {
            var tags = new TagList
            {
                { "media.operation", "save" },
                { "result.status", "success" }
            };

            MediaFilesSaved.Add(1, tags);
            MediaFileSize.Record(Math.Max(0, bytes), tags);
        }

        public static void RecordMediaFailure(string operation, string errorType)
            => MediaFailures.Add(1, new TagList
            {
                { "media.operation", NormalizeMediaOperation(operation) },
                { "result.status", "failure" },
                { "error.type", NormalizeErrorType(errorType) }
            });

        public static void RecordQrCodeGenerated(string format)
            => QrCodeGenerated.Add(1, new TagList
            {
                { "qr.format", NormalizeQrFormat(format) },
                { "result.status", "success" }
            });

        public static void RecordQrCodeFailure(string format, string errorType)
            => QrCodeFailures.Add(1, new TagList
            {
                { "qr.format", NormalizeQrFormat(format) },
                { "result.status", "failure" },
                { "error.type", NormalizeErrorType(errorType) }
            });

        private static TagList CacheTags(string operation)
            => new()
            {
                { "cache.operation", operation }
            };

        private static string NormalizeRequestKind(string value)
            => value is "command" or "query" or "request" ? value : "request";

        private static string NormalizeResultStatus(string value)
            => value is "success" or "failure" or "cancelled" ? value : "failure";

        private static string NormalizeMediaOperation(string value)
            => value is "save" or "delete" ? value : "operation";

        private static string NormalizeQrFormat(string value)
            => value is "png" or "svg" ? value : "unknown";

        private static string NormalizeErrorType(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "validation" => "validation",
                "infrastructure" => "infrastructure",
                "conflict" => "conflict",
                "notfound" => "not_found",
                "unauthorized" => "unauthorized",
                "forbidden" => "forbidden",
                "cancelled" => "cancelled",
                "authentication" => "authentication",
                "timeout" => "timeout",
                "send" => "send",
                "connection" => "connection",
                _ => "unknown"
            };
        }
    }
}
