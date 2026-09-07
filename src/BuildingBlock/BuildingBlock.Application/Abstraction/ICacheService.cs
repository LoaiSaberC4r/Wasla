namespace BuildingBlock.Application.Abstraction
{
    public interface ICacheService
    {
        Task<(bool found, T? value)> TryGetAsync<T>(string key, CancellationToken ct = default);

        Task SetAsync<T>(string key, T value, TimeSpan ttl, IEnumerable<string> tags, CancellationToken ct = default);

        Task RemoveAsync(string key, CancellationToken ct = default);

        Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken ct = default);

        Task ClearAllAsync(CancellationToken ct = default);
    }
}
