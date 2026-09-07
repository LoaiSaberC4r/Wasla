using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Caching;
using BuildingBlock.Infrastructure.Service;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections;
using System.Globalization;
using System.Reflection;

namespace BuildingBlock.Tests;

public sealed class Phase3CacheKeyAndConsistencyTests
{
    [Fact]
    public void Canonical_keys_are_stable_for_complex_objects_dictionaries_and_sets()
    {
        var provider = new ScopeProvider("alpha");
        var first = new ComplexRequest(
            new ComplexValue("Ada", new[] { 3, 1, 2 }),
            new Dictionary<string, int> { ["b"] = 2, ["a"] = 1 },
            new HashSet<string> { "west", "east" });
        var reordered = new ComplexRequest(
            new ComplexValue("Ada", new[] { 3, 1, 2 }),
            new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 },
            new HashSet<string> { "east", "west" });
        var changed = first with { Value = new ComplexValue("Grace", new[] { 3, 1, 2 }) };

        Assert.True(CacheKeyFactory.TryBuild(first, first, provider, out var firstKey, out _));
        Assert.True(CacheKeyFactory.TryBuild(reordered, reordered, provider, out var reorderedKey, out _));
        Assert.True(CacheKeyFactory.TryBuild(changed, changed, provider, out var changedKey, out _));

        Assert.Equal(firstKey, reorderedKey);
        Assert.NotEqual(firstKey, changedKey);
        Assert.StartsWith("bb:ComplexRequest:", firstKey, StringComparison.Ordinal);
    }

    [Fact]
    public void Ordered_lists_null_numeric_types_and_utc_values_have_deliberate_identity()
    {
        AssertSerializedDifferent(new[] { 1, 2 }, new[] { 2, 1 });
        AssertSerializedDifferent(null, string.Empty);
        AssertSerializedDifferent(1, 1L);

        var instant = new DateTime(2026, 7, 21, 9, 30, 0, DateTimeKind.Utc);
        var unspecifiedSameInstant = DateTime.SpecifyKind(instant, DateTimeKind.Unspecified);
        Assert.True(CanonicalCacheKeySerializer.TrySerialize(instant, out var utc, out _));
        Assert.True(CanonicalCacheKeySerializer.TrySerialize(unspecifiedSameInstant, out var unspecified, out _));
        Assert.Equal(utc, unspecified);
    }

    [Fact]
    public void Public_properties_are_written_in_ordinal_order_not_declaration_order()
    {
        Assert.True(CanonicalCacheKeySerializer.TrySerialize(
            new DeclarationOrderProbe { Zulu = "z", Alpha = "a" },
            out var payload,
            out _));

        Assert.True(
            payload.IndexOf("p:5:Alpha", StringComparison.Ordinal) <
            payload.IndexOf("p:4:Zulu", StringComparison.Ordinal));
    }

    [Fact]
    public void Cycles_depth_collection_bounds_and_throwing_getters_skip_without_partial_keys()
    {
        var cycle = new Node();
        cycle.Next = cycle;
        AssertSkipped(new ObjectRequest(cycle), "cache-key-cycle-detected");

        var root = new Node();
        var current = root;
        for (var index = 0; index < CanonicalCacheKeySerializer.MaximumDepth + 2; index++)
        {
            current.Next = new Node();
            current = current.Next;
        }

        AssertSkipped(new ObjectRequest(root), "cache-key-depth-limit");
        AssertSkipped(
            new ObjectRequest(Enumerable.Range(0, CanonicalCacheKeySerializer.MaximumCollectionItems + 1).ToArray()),
            "cache-key-collection-limit");
        AssertSkipped(new ThrowingRequest(), "cache-key-value-unreadable");
        AssertSkipped(new ThrowingCacheComponentRequest(), "cache-key-value-unreadable");
    }

    [Fact]
    public void Sensitive_inputs_skip_unless_explicitly_ignored_and_raw_values_never_enter_final_key()
    {
        AssertSkipped(new SensitiveRequest("correct horse battery staple"), "sensitive-cache-input");

        var ignored = new IgnoredSensitiveRequest(
            "correct horse battery staple",
            "person@example.test",
            "private search text");
        Assert.True(CacheKeyFactory.TryBuild(
            ignored,
            ignored,
            new ScopeProvider("alpha"),
            out var key,
            out _));

        Assert.DoesNotContain("correct horse", key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("person@example.test", key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private search text", key, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3 + nameof(IgnoredSensitiveRequest).Length + 1 + 64, key.Length);
    }

    [Fact]
    public void Culture_and_every_resolved_scope_component_isolated_keys_while_missing_scope_fails_closed()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            var request = new ScopedRequest("same", CacheScope.User);
            var provider = new ScopeProvider("one");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.True(CacheKeyFactory.TryBuild(request, request, provider, out var first, out _));

            provider.Value = "two";
            Assert.True(CacheKeyFactory.TryBuild(request, request, provider, out var second, out _));
            Assert.NotEqual(first, second);

            foreach (var scope in new[] { CacheScope.Context, CacheScope.Tenant })
            {
                var scoped = request with { Scope = scope };
                Assert.True(CacheKeyFactory.TryBuild(scoped, scoped, provider, out var scopedKey, out _));
                Assert.NotEqual(second, scopedKey);
            }

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar-EG");
            Assert.True(CacheKeyFactory.TryBuild(request, request, provider, out var arabic, out _));
            Assert.NotEqual(second, arabic);

            provider.Available = false;
            Assert.False(CacheKeyFactory.TryBuild(request, request, provider, out var missing, out var reason));
            Assert.Empty(missing);
            Assert.Equal("cache-scope-unavailable", reason);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public async Task Cancellation_after_tag_invalidation_transition_completes_consistently_and_later_set_survives()
    {
        using var blockingCache = new BlockingMemoryCache();
        using var cache = new MemoryCacheService(blockingCache, NullLogger<MemoryCacheService>.Instance);
        await cache.SetAsync(
            "key",
            "old",
            TimeSpan.FromMinutes(1),
            new[] { "tag" },
            TestContext.Current.CancellationToken);

        blockingCache.BlockNextRemoval();
        using var cancellation = new CancellationTokenSource();
        var invalidation = Task.Run(() =>
            cache.InvalidateByTagsAsync(new[] { "tag" }, cancellation.Token),
            TestContext.Current.CancellationToken);
        Assert.True(blockingCache.WaitUntilRemovalStarts());

        await cancellation.CancelAsync();
        var laterSet = cache.SetAsync(
            "key",
            "new",
            TimeSpan.FromMinutes(1),
            new[] { "new-tag" },
            TestContext.Current.CancellationToken);
        blockingCache.ReleaseRemoval();

        await invalidation;
        await laterSet;

        var current = await cache.TryGetAsync<string>("key", TestContext.Current.CancellationToken);
        Assert.True(current.found);
        Assert.Equal("new", current.value);
        Assert.Equal(new[] { "key" }, GetTrackedKeys(cache));
    }

    [Fact]
    public async Task Cancellation_after_clear_transition_finishes_and_does_not_corrupt_later_registrations()
    {
        using var blockingCache = new BlockingMemoryCache();
        using var cache = new MemoryCacheService(blockingCache, NullLogger<MemoryCacheService>.Instance);
        await cache.SetAsync("first", 1, TimeSpan.FromMinutes(1), new[] { "shared" }, TestContext.Current.CancellationToken);
        await cache.SetAsync("second", 2, TimeSpan.FromMinutes(1), new[] { "shared" }, TestContext.Current.CancellationToken);

        blockingCache.BlockNextRemoval();
        using var cancellation = new CancellationTokenSource();
        var clear = Task.Run(
            () => cache.ClearAllAsync(cancellation.Token),
            TestContext.Current.CancellationToken);
        Assert.True(blockingCache.WaitUntilRemovalStarts());

        await cancellation.CancelAsync();
        var laterSet = cache.SetAsync(
            "later",
            3,
            TimeSpan.FromMinutes(1),
            new[] { "shared" },
            TestContext.Current.CancellationToken);
        blockingCache.ReleaseRemoval();

        await clear;
        await laterSet;

        Assert.False((await cache.TryGetAsync<int>("first", TestContext.Current.CancellationToken)).found);
        Assert.False((await cache.TryGetAsync<int>("second", TestContext.Current.CancellationToken)).found);
        Assert.True((await cache.TryGetAsync<int>("later", TestContext.Current.CancellationToken)).found);
        Assert.Equal(new[] { "later" }, GetTrackedKeys(cache));
    }

    [Fact]
    public async Task Cancellation_before_cache_mutation_leaves_entries_and_indexes_unchanged()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cache = new MemoryCacheService(memory, NullLogger<MemoryCacheService>.Instance);
        await cache.SetAsync("key", "value", TimeSpan.FromMinutes(1), new[] { "tag" }, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.InvalidateByTagsAsync(new[] { "tag" }, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.ClearAllAsync(cancellation.Token));

        Assert.True((await cache.TryGetAsync<string>("key", TestContext.Current.CancellationToken)).found);
        Assert.Equal(new[] { "key" }, GetTrackedKeys(cache));
    }

    private static void AssertSerializedDifferent(object? left, object? right)
    {
        Assert.True(CanonicalCacheKeySerializer.TrySerialize(left, out var leftPayload, out _));
        Assert.True(CanonicalCacheKeySerializer.TrySerialize(right, out var rightPayload, out _));
        Assert.NotEqual(leftPayload, rightPayload);
    }

    private static void AssertSkipped(ICacheableRequest request, string expectedReason)
    {
        Assert.False(CacheKeyFactory.TryBuild(
            request,
            request,
            new ScopeProvider("alpha"),
            out var key,
            out var reason));
        Assert.Empty(key);
        Assert.Equal(expectedReason, reason);
    }

    private static string[] GetTrackedKeys(MemoryCacheService cache)
    {
        var registrations = typeof(MemoryCacheService)
            .GetField("_registrations", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cache) as IDictionary;
        return registrations!.Keys.Cast<string>().Order(StringComparer.Ordinal).ToArray();
    }

    private sealed record ComplexRequest(
        ComplexValue Value,
        Dictionary<string, int> Map,
        HashSet<string> Set) : ICacheableRequest;

    private sealed record ComplexValue(string Name, IReadOnlyList<int> Values);

    private sealed record ObjectRequest(object Value) : ICacheableRequest;

    private sealed record SensitiveRequest(string Password) : ICacheableRequest;

    private sealed record IgnoredSensitiveRequest(
        [property: CacheKeyIgnore] string Password,
        string Email,
        string SearchText) : ICacheableRequest;

    private sealed record ScopedRequest(string Value, CacheScope Scope) : ICacheableRequest;

    private sealed class ThrowingRequest : ICacheableRequest
    {
        public string Broken => throw new InvalidOperationException("sensitive getter detail");
    }

    private sealed class ThrowingCacheComponentRequest : ICacheableRequest
    {
        public string? CacheKey => throw new InvalidOperationException("cache component detail");
    }

    private sealed class DeclarationOrderProbe
    {
        public string Zulu { get; init; } = string.Empty;
        public string Alpha { get; init; } = string.Empty;
    }

    private sealed class Node
    {
        public Node? Next { get; set; }
    }

    private sealed class ScopeProvider : ICacheScopeValueProvider
    {
        public ScopeProvider(string value)
        {
            Value = value;
        }

        public bool Available { get; set; } = true;
        public string Value { get; set; }

        public bool TryGetScopeValue(CacheScope scope, out string value)
        {
            value = Available ? $"{scope}:{Value}" : string.Empty;
            return Available;
        }
    }

    private sealed class BlockingMemoryCache : IMemoryCache
    {
        private readonly MemoryCache _inner = new(new MemoryCacheOptions());
        private readonly ManualResetEventSlim _removalStarted = new(initialState: false);
        private readonly ManualResetEventSlim _releaseRemoval = new(initialState: false);
        private int _blockNextRemoval;

        public ICacheEntry CreateEntry(object key) => _inner.CreateEntry(key);

        public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);

        public void Remove(object key)
        {
            if (Interlocked.Exchange(ref _blockNextRemoval, 0) == 1)
            {
                _removalStarted.Set();
                _releaseRemoval.Wait(TimeSpan.FromSeconds(10));
            }

            _inner.Remove(key);
        }

        public void BlockNextRemoval()
        {
            _removalStarted.Reset();
            _releaseRemoval.Reset();
            Volatile.Write(ref _blockNextRemoval, 1);
        }

        public bool WaitUntilRemovalStarts()
            => _removalStarted.Wait(TimeSpan.FromSeconds(10));

        public void ReleaseRemoval() => _releaseRemoval.Set();

        public void Dispose()
        {
            _inner.Dispose();
            _removalStarted.Dispose();
            _releaseRemoval.Dispose();
        }
    }
}
