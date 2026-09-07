using System.Text.Json.Serialization;

namespace BuildingBlock.Api.ProblemDetails
{
    internal sealed record ProblemDetailsErrorPayload(
        string Code,
        string Message,
        string Type,
        string? Details,
        string? Source,
        double? RetryAfter);

    [JsonSerializable(typeof(ProblemDetailsErrorPayload[]))]
    internal sealed partial class BuildingBlockProblemDetailsJsonContext : JsonSerializerContext
    {
    }
}
