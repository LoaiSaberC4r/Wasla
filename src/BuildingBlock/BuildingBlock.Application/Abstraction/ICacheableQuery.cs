using System.Text.Json.Serialization;

namespace BuildingBlock.Application.Abstraction
{
    public enum CacheScope
    {
        Global = 0,
        User = 1,
        Context = 2,
        Tenant = 3
    }

    public interface ICacheableRequest
    {
        [JsonIgnore]
        string? CacheKey => null;

        [JsonIgnore]
        TimeSpan? TimeToLive => null;

        [JsonIgnore]
        IEnumerable<string> Tags => Enumerable.Empty<string>();

        [JsonIgnore]
        CacheScope Scope => CacheScope.Global;
    }

    public interface ICacheableQuery<TResponse> : IQuery<TResponse>, ICacheableRequest
    {
    }
}
