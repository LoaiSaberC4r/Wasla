using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Behaviors;
using BuildingBlock.Application.Diagnostics;
using BuildingBlock.Domain.Results;
using System.Diagnostics;

namespace BuildingBlock.Tests;

public sealed class Phase4TracingTests
{
    [Fact]
    public async Task Command_activity_uses_central_source_and_stable_name()
    {
        using var collector = new ActivityCollector();
        var behavior = new TracingBehavior<TestCommand, Result>();

        var result = await behavior.Handle(
            new TestCommand("secret-value"),
            _ => Task.FromResult(Result.Ok()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var activity = Assert.Single(collector.Stopped);
        Assert.Equal(BuildingBlockDiagnostics.ActivitySourceName, activity.Source.Name);
        Assert.Equal("buildingblock.request", activity.OperationName);
        Assert.Equal("command", Tag(activity, "buildingblock.request.kind"));
        Assert.Equal(typeof(TestCommand).FullName, Tag(activity, "buildingblock.request.type"));
        Assert.Equal("success", Tag(activity, "buildingblock.result.status"));
        Assert.DoesNotContain(activity.Tags, tag => tag.Value == "secret-value");
    }

    [Fact]
    public async Task Query_failure_result_records_safe_error_tags()
    {
        using var collector = new ActivityCollector();
        var behavior = new TracingBehavior<TestQuery, Result<string>>();

        var result = await behavior.Handle(
            new TestQuery(),
            _ => Task.FromResult(Result<string>.Fail(Error.Domain("Domain.Failed", "Sensitive message"))),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        var activity = Assert.Single(collector.Stopped);
        Assert.Equal("query", Tag(activity, "buildingblock.request.kind"));
        Assert.Equal("failure", Tag(activity, "buildingblock.result.status"));
        Assert.Equal("Domain.Failed", Tag(activity, "buildingblock.error.code"));
        Assert.Equal("Domain", Tag(activity, "buildingblock.error.type"));
        Assert.DoesNotContain(activity.Tags, tag => tag.Value == "Sensitive message");
    }

    [Fact]
    public async Task Exception_records_type_without_message()
    {
        using var collector = new ActivityCollector();
        var behavior = new TracingBehavior<TestCommand, Result>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new TestCommand("secret-value"),
            _ => Task.FromException<Result>(new InvalidOperationException("do not tag me")),
            CancellationToken.None));

        var activity = Assert.Single(collector.Stopped);
        Assert.Equal("failure", Tag(activity, "buildingblock.result.status"));
        Assert.Equal("InvalidOperationException", Tag(activity, "buildingblock.error.type"));
        Assert.DoesNotContain(activity.Tags, tag => tag.Value == "do not tag me");
        Assert.DoesNotContain(activity.Events.SelectMany(e => e.Tags), tag => tag.Key == "exception.message");
        Assert.Contains(activity.Events.SelectMany(e => e.Tags), tag => tag.Key == "exception.type");
    }

    [Fact]
    public async Task Cancellation_is_distinguished_from_failure()
    {
        using var collector = new ActivityCollector();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var behavior = new TracingBehavior<TestCommand, Result>();

        await Assert.ThrowsAsync<OperationCanceledException>(() => behavior.Handle(
            new TestCommand("secret-value"),
            _ => Task.FromException<Result>(new OperationCanceledException(cts.Token)),
            cts.Token));

        var activity = Assert.Single(collector.Stopped);
        Assert.Equal("cancelled", Tag(activity, "buildingblock.result.status"));
        Assert.DoesNotContain(activity.Tags, tag => tag.Key == "buildingblock.error.type");
    }

    private static string? Tag(Activity activity, string key)
        => activity.Tags.SingleOrDefault(tag => tag.Key == key).Value;

    private sealed class ActivityCollector : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly List<Activity> _stopped = new();

        public ActivityCollector()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == BuildingBlockDiagnostics.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => _stopped.Add(activity)
            };

            ActivitySource.AddActivityListener(_listener);
        }

        public IReadOnlyList<Activity> Stopped => _stopped;

        public void Dispose() => _listener.Dispose();
    }

    private sealed record TestCommand(string Password) : ICommand;

    private sealed record TestQuery : IQuery<string>;
}
