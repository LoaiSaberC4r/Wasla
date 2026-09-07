using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Caching;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Behaviors;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Service;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;

namespace BuildingBlock.Tests;

public sealed class CacheTests
{
    [Fact]
    public async Task Cacheable_query_is_detected_when_response_is_result_of_t_and_hit_avoids_handler()
    {
        var behavior = CreateBehavior<CachedQuery>(new FakeCurrentUser());
        var request = new CachedQuery("same");
        var calls = 0;

        var first = await behavior.Handle(request, _ =>
        {
            calls++;
            return Task.FromResult(Result<string>.Ok("value"));
        }, CancellationToken.None);

        var second = await behavior.Handle(request, _ =>
        {
            calls++;
            return Task.FromResult(Result<string>.Ok("other"));
        }, CancellationToken.None);

        Assert.Equal("value", first.Value);
        Assert.Equal("value", second.Value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Failure_results_are_not_cached()
    {
        var behavior = CreateBehavior<CachedQuery>(new FakeCurrentUser());
        var calls = 0;

        await behavior.Handle(new CachedQuery("failure"), _ =>
        {
            calls++;
            return Task.FromResult(Result<string>.Fail(Error.Domain("Domain.Failed", "failed")));
        }, CancellationToken.None);

        await behavior.Handle(new CachedQuery("failure"), _ =>
        {
            calls++;
            return Task.FromResult(Result<string>.Ok("success"));
        }, CancellationToken.None);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Culture_user_and_context_scopes_separate_cache_entries_and_missing_scope_skips_caching()
    {
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            var user = new FakeCurrentUser
            {
                UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
            };
            var provider = new MutableCacheScopeValueProvider(user)
            {
                ContextValue = "context:alpha"
            };
            var behavior = CreateBehavior<ScopedCachedQuery>(provider);

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var en = await behavior.Handle(new ScopedCachedQuery("scope", CacheScope.User), _ => Task.FromResult(Result<string>.Ok("en")), CancellationToken.None);

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar");
            var ar = await behavior.Handle(new ScopedCachedQuery("scope", CacheScope.User), _ => Task.FromResult(Result<string>.Ok("ar")), CancellationToken.None);

            user.UserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
            var otherUser = await behavior.Handle(new ScopedCachedQuery("scope", CacheScope.User), _ => Task.FromResult(Result<string>.Ok("other-user")), CancellationToken.None);

            var context = await behavior.Handle(new ScopedCachedQuery("scope", CacheScope.Context), _ => Task.FromResult(Result<string>.Ok("context")), CancellationToken.None);

            provider.ContextValue = null;
            var skippedOne = await behavior.Handle(new ScopedCachedQuery("missing", CacheScope.Context), _ => Task.FromResult(Result<string>.Ok("first")), CancellationToken.None);
            var skippedTwo = await behavior.Handle(new ScopedCachedQuery("missing", CacheScope.Context), _ => Task.FromResult(Result<string>.Ok("second")), CancellationToken.None);

            Assert.Equal("en", en.Value);
            Assert.Equal("ar", ar.Value);
            Assert.Equal("other-user", otherUser.Value);
            Assert.Equal("context", context.Value);
            Assert.Equal("first", skippedOne.Value);
            Assert.Equal("second", skippedTwo.Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = culture;
        }
    }

    [Fact]
    public async Task Tag_invalidation_expiration_and_keyed_lock_work()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memoryCache, NullLogger<MemoryCacheService>.Instance);
        await cache.SetAsync("key", "value", TimeSpan.FromMilliseconds(50), new[] { "Tag" }, TestContext.Current.CancellationToken);

        var hit = await cache.TryGetAsync<string>("key", TestContext.Current.CancellationToken);
        Assert.True(hit.found);

        await cache.InvalidateByTagsAsync(new[] { "tag" }, TestContext.Current.CancellationToken);
        var miss = await cache.TryGetAsync<string>("key", TestContext.Current.CancellationToken);
        Assert.False(miss.found);

        await cache.SetAsync("expiring", "value", TimeSpan.FromMilliseconds(10), new[] { "exp" }, TestContext.Current.CancellationToken);
        await Task.Delay(80, TestContext.Current.CancellationToken);
        _ = await cache.TryGetAsync<string>("expiring", TestContext.Current.CancellationToken);
        await cache.InvalidateByTagsAsync(new[] { "exp" }, TestContext.Current.CancellationToken);

        var behavior = CreateBehavior<CachedQuery>(new FakeCurrentUser());
        var calls = 0;
        var request = new CachedQuery("concurrent");
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => behavior.Handle(request, async cancellationToken =>
            {
                Interlocked.Increment(ref calls);
                await Task.Delay(50, cancellationToken);
                return Result<string>.Ok("loaded");
            }, CancellationToken.None))
            .ToArray();

        await Task.WhenAll(tasks);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Clear_all_tracks_entries_without_tags_and_tag_invalidation_leaves_untagged_entries()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memoryCache, NullLogger<MemoryCacheService>.Instance);

        await cache.SetAsync("untagged", "value", TimeSpan.FromMinutes(1), Array.Empty<string>(), TestContext.Current.CancellationToken);
        await cache.SetAsync("tagged", "value", TimeSpan.FromMinutes(1), new[] { "records" }, TestContext.Current.CancellationToken);

        await cache.InvalidateByTagsAsync(new[] { "unrelated" }, TestContext.Current.CancellationToken);
        Assert.True((await cache.TryGetAsync<string>("untagged", TestContext.Current.CancellationToken)).found);
        Assert.True((await cache.TryGetAsync<string>("tagged", TestContext.Current.CancellationToken)).found);

        await cache.InvalidateByTagsAsync(new[] { "records" }, TestContext.Current.CancellationToken);
        Assert.True((await cache.TryGetAsync<string>("untagged", TestContext.Current.CancellationToken)).found);
        Assert.False((await cache.TryGetAsync<string>("tagged", TestContext.Current.CancellationToken)).found);

        await cache.ClearAllAsync(TestContext.Current.CancellationToken);
        Assert.False((await cache.TryGetAsync<string>("untagged", TestContext.Current.CancellationToken)).found);
    }

    [Fact]
    public async Task Stale_eviction_callback_cannot_remove_replacement_tag_indexes()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memoryCache, NullLogger<MemoryCacheService>.Instance);

        await cache.SetAsync("key", "v1", TimeSpan.FromMinutes(1), new[] { "A" }, TestContext.Current.CancellationToken);
        await cache.SetAsync("key", "v2", TimeSpan.FromMinutes(1), new[] { "B" }, TestContext.Current.CancellationToken);

        InvokeEvictionCleanup(cache, "key", version: 1);

        await cache.InvalidateByTagsAsync(new[] { "A" }, TestContext.Current.CancellationToken);
        var afterOldTag = await cache.TryGetAsync<string>("key", TestContext.Current.CancellationToken);
        Assert.True(afterOldTag.found);
        Assert.Equal("v2", afterOldTag.value);

        await cache.InvalidateByTagsAsync(new[] { "B" }, TestContext.Current.CancellationToken);
        Assert.False((await cache.TryGetAsync<string>("key", TestContext.Current.CancellationToken)).found);
    }

    [Fact]
    public async Task Parallel_sets_keep_one_current_registration_that_can_be_invalidated()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memoryCache, NullLogger<MemoryCacheService>.Instance);

        await Task.WhenAll(Enumerable.Range(0, 64)
            .Select(index => cache.SetAsync(
                "key",
                index,
                TimeSpan.FromMinutes(1),
                new[] { $"tag:{index}" })));

        var current = await cache.TryGetAsync<int>("key", TestContext.Current.CancellationToken);
        Assert.True(current.found);

        await cache.InvalidateByTagsAsync(new[] { $"tag:{current.value}" }, TestContext.Current.CancellationToken);

        Assert.False((await cache.TryGetAsync<int>("key", TestContext.Current.CancellationToken)).found);
        Assert.Empty(GetTrackedKeys(cache));
    }

    [Fact]
    public async Task Parallel_cache_mutations_do_not_corrupt_tag_indexes()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memoryCache, NullLogger<MemoryCacheService>.Instance);
        var start = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, 128)
            .Select(index => Task.Run(async () =>
            {
                await start.Task;
                var key = $"key:{index % 8}";
                var tag = $"tag:{index % 4}";

                switch (index % 4)
                {
                    case 0:
                        await cache.SetAsync(key, index, TimeSpan.FromMinutes(1), new[] { tag, "shared" });
                        break;
                    case 1:
                        await cache.InvalidateByTagsAsync(new[] { tag });
                        break;
                    case 2:
                        await cache.RemoveAsync(key);
                        break;
                    default:
                        await cache.SetAsync(key, index, TimeSpan.FromMinutes(1), Array.Empty<string>());
                        break;
                }
            }))
            .ToArray();

        start.SetResult(null);
        await Task.WhenAll(tasks);

        foreach (var key in GetTrackedKeys(cache))
        {
            Assert.True(memoryCache.TryGetValue(key, out _));
        }
    }

    [Fact]
    public async Task Clear_all_removes_tracked_entries_and_allows_later_sets()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memoryCache, NullLogger<MemoryCacheService>.Instance);

        await cache.SetAsync("before", "value", TimeSpan.FromMinutes(1), new[] { "clear" }, TestContext.Current.CancellationToken);
        await cache.ClearAllAsync(TestContext.Current.CancellationToken);
        Assert.False((await cache.TryGetAsync<string>("before", TestContext.Current.CancellationToken)).found);
        Assert.Empty(GetTrackedKeys(cache));

        await cache.SetAsync("after", "value", TimeSpan.FromMinutes(1), new[] { "clear" }, TestContext.Current.CancellationToken);
        Assert.True((await cache.TryGetAsync<string>("after", TestContext.Current.CancellationToken)).found);

        await cache.InvalidateByTagsAsync(new[] { "clear" }, TestContext.Current.CancellationToken);
        Assert.False((await cache.TryGetAsync<string>("after", TestContext.Current.CancellationToken)).found);
    }

    private static QueryCacheBehavior<TRequest, Result<string>> CreateBehavior<TRequest>(ICurrentUser currentUser)
        where TRequest : notnull, IRequest<Result<string>>
        => CreateBehavior<TRequest>(new DefaultCacheScopeValueProvider(currentUser));

    private static QueryCacheBehavior<TRequest, Result<string>> CreateBehavior<TRequest>(ICacheScopeValueProvider scopeValueProvider)
        where TRequest : notnull, IRequest<Result<string>>
    {
        var cache = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()), NullLogger<MemoryCacheService>.Instance);
        return new QueryCacheBehavior<TRequest, Result<string>>(
            cache,
            new KeyedSemaphore(),
            scopeValueProvider,
            NullLogger<QueryCacheBehavior<TRequest, Result<string>>>.Instance);
    }

    private sealed record CachedQuery(string Value) : ICacheableQuery<string>
    {
        public IEnumerable<string> Tags => new[] { "queries" };
    }

    private sealed record ScopedCachedQuery(string Value, CacheScope ScopeValue) : ICacheableQuery<string>
    {
        public CacheScope Scope => ScopeValue;
        public IEnumerable<string> Tags => new[] { "scoped" };
    }

    private sealed class MutableCacheScopeValueProvider : ICacheScopeValueProvider
    {
        private readonly ICurrentUser _currentUser;

        public MutableCacheScopeValueProvider(ICurrentUser currentUser)
        {
            _currentUser = currentUser;
        }

        public string? ContextValue { get; set; }

        public bool TryGetScopeValue(CacheScope scope, out string value)
        {
            value = string.Empty;

            switch (scope)
            {
                case CacheScope.Global:
                    value = "global";
                    return true;

                case CacheScope.User when _currentUser.UserId is { } userId:
                    value = $"user:{userId:N}";
                    return true;

                case CacheScope.Context when !string.IsNullOrWhiteSpace(ContextValue):
                    value = ContextValue;
                    return true;

                default:
                    return false;
            }
        }
    }

    private static void InvokeEvictionCleanup(MemoryCacheService cache, string key, long version)
    {
        var method = typeof(MemoryCacheService).GetMethod(
            "RemoveIndexesForEviction",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Eviction cleanup method was not found.");

        method.Invoke(cache, new object[] { key, version });
    }

    private static IReadOnlyCollection<string> GetTrackedKeys(MemoryCacheService cache)
    {
        var field = typeof(MemoryCacheService).GetField(
            "_registrations",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Registration field was not found.");

        var registrations = (System.Collections.IDictionary)field.GetValue(cache)!;
        return registrations.Keys.Cast<string>().ToArray();
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => UserId.HasValue;
        public Guid? UserId { get; set; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = Array.Empty<string>();
        public IReadOnlyCollection<string> Permissions { get; } = Array.Empty<string>();
        public string? GetClaimValue(string claimType) => null;
        public IReadOnlyCollection<string> GetClaimValues(string claimType) => Array.Empty<string>();
    }
}
