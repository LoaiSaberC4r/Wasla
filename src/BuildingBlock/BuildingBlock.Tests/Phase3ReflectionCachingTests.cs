using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Caching;
using BuildingBlock.Application.Abstraction.Results;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BuildingBlock.Tests;

public sealed class Phase3ReflectionCachingTests
{
    [Fact]
    public void Result_failure_delegates_are_cached_per_response_type()
    {
        var responseType = typeof(Result<SingleErrorProbe>);
        var error = Error.Domain("Domain.Single", "single");

        var before = ResultFailureFactory.SingleErrorFactoryCount;

        var first = ResultFailureFactory.Create(responseType, error);
        var afterFirst = ResultFailureFactory.SingleErrorFactoryCount;
        var second = ResultFailureFactory.Create(responseType, error);
        var afterSecond = ResultFailureFactory.SingleErrorFactoryCount;

        Assert.IsType<Result<SingleErrorProbe>>(first);
        Assert.IsType<Result<SingleErrorProbe>>(second);
        Assert.True(afterFirst >= before + 1);
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public void Result_failure_collection_delegates_are_cached_per_response_type()
    {
        var responseType = typeof(Result<ManyErrorProbe>);
        var errors = new[] { Error.Domain("Domain.Many", "many") };

        var before = ResultFailureFactory.ManyErrorFactoryCount;

        var first = ResultFailureFactory.Create(responseType, errors);
        var afterFirst = ResultFailureFactory.ManyErrorFactoryCount;
        var second = ResultFailureFactory.Create(responseType, errors);
        var afterSecond = ResultFailureFactory.ManyErrorFactoryCount;

        Assert.IsType<Result<ManyErrorProbe>>(first);
        Assert.IsType<Result<ManyErrorProbe>>(second);
        Assert.True(afterFirst >= before + 1);
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public void Result_success_accessor_is_cached_per_result_type()
    {
        var value = Result<InspectorProbe>.Ok(new InspectorProbe());
        var before = ResultInspector.SuccessAccessorCount;

        Assert.True(ResultInspector.IsSuccess(value));
        var afterFirst = ResultInspector.SuccessAccessorCount;
        Assert.True(ResultInspector.IsSuccess(value));
        var afterSecond = ResultInspector.SuccessAccessorCount;

        Assert.True(afterFirst >= before + 1);
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public async Task Domain_event_dispatch_delegate_is_cached_and_handlers_run_in_registration_order()
    {
        var order = new List<string>();
        using var provider = new ServiceCollection()
            .AddSingleton(order)
            .AddTransient<IDomainEventHandler<CachedEvent>, FirstCachedEventHandler>()
            .AddTransient<IDomainEventHandler<CachedEvent>, SecondCachedEventHandler>()
            .BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(provider);
        var before = DomainEventDispatcher.DispatchDelegateCount;

        await dispatcher.DispatchAsync(new CachedEvent(), CancellationToken.None);
        var afterFirst = DomainEventDispatcher.DispatchDelegateCount;
        await dispatcher.DispatchAsync(new CachedEvent(), CancellationToken.None);
        var afterSecond = DomainEventDispatcher.DispatchDelegateCount;

        Assert.Equal(new[] { "first", "second", "first", "second" }, order);
        Assert.True(afterFirst >= before + 1);
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public async Task Domain_event_handler_exceptions_are_not_wrapped_in_target_invocation_exception()
    {
        using var provider = new ServiceCollection()
            .AddTransient<IDomainEventHandler<ThrowingEvent>, ThrowingEventHandler>()
            .BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(provider);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(new ThrowingEvent(), CancellationToken.None));

        Assert.Equal("handler failed", exception.Message);
        Assert.IsNotType<TargetInvocationException>(exception);
    }

    private sealed class SingleErrorProbe
    {
    }

    private sealed class ManyErrorProbe
    {
    }

    private sealed class InspectorProbe
    {
    }

    private sealed record CachedEvent : IDomainEvent;

    private sealed record ThrowingEvent : IDomainEvent;

    private sealed class FirstCachedEventHandler : IDomainEventHandler<CachedEvent>
    {
        private readonly List<string> _order;

        public FirstCachedEventHandler(List<string> order)
        {
            _order = order;
        }

        public Task Handle(CachedEvent domainEvent, CancellationToken ct)
        {
            _order.Add("first");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondCachedEventHandler : IDomainEventHandler<CachedEvent>
    {
        private readonly List<string> _order;

        public SecondCachedEventHandler(List<string> order)
        {
            _order = order;
        }

        public Task Handle(CachedEvent domainEvent, CancellationToken ct)
        {
            _order.Add("second");
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingEventHandler : IDomainEventHandler<ThrowingEvent>
    {
        public Task Handle(ThrowingEvent domainEvent, CancellationToken ct)
            => throw new InvalidOperationException("handler failed");
    }
}
