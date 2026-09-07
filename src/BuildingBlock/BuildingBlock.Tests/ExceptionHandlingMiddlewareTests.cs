using BuildingBlock.Api.Middleware;
using BuildingBlock.Api.ProblemDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace BuildingBlock.Tests;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task Client_aborted_cancellation_is_not_logged_or_converted_to_500()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = new DefaultHttpContext
        {
            RequestAborted = cancellation.Token
        };
        var logger = new CountingLogger();
        var middleware = CreateMiddleware(
            _ => throw new OperationCanceledException(cancellation.Token),
            logger);

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, logger.ErrorCount);
    }

    [Fact]
    public async Task Unrelated_operation_cancellation_is_allowed_to_propagate()
    {
        var context = new DefaultHttpContext();
        var logger = new CountingLogger();
        var middleware = CreateMiddleware(
            _ => throw new OperationCanceledException("unrelated timeout"),
            logger);

        await Assert.ThrowsAsync<OperationCanceledException>(() => middleware.Invoke(context));

        Assert.Equal(0, logger.ErrorCount);
    }

    [Fact]
    public async Task Exception_before_response_start_is_logged_once_and_written_as_problem_details()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBuildingBlockProblemDetails();
        await using var provider = services.BuildServiceProvider();
        await using var responseBody = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };
        context.Response.Body = responseBody;
        context.Request.Path = "/before-start";
        var logger = new CountingLogger();
        var middleware = CreateMiddleware(
            _ => throw new InvalidOperationException("unexpected"),
            logger);

        await middleware.Invoke(context);
        responseBody.Position = 0;
        var body = await new StreamReader(responseBody, Encoding.UTF8).ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Contains("Common.Unknown", body, StringComparison.Ordinal);
        Assert.Equal(1, logger.ErrorCount);
    }

    [Fact]
    public async Task Exception_after_response_start_is_logged_once_and_rethrown_without_second_body()
    {
        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        var context = new DefaultHttpContext(features);
        var logger = new CountingLogger();
        var middleware = CreateMiddleware(
            _ => throw new InvalidOperationException("after start"),
            logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.Invoke(context));

        Assert.True(context.Response.HasStarted);
        Assert.Equal(1, logger.ErrorCount);
    }

    private static ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next, CountingLogger logger)
        => new(next, new ProblemDetailsMapper(), logger);

    private sealed class CountingLogger : ILogger<ExceptionHandlingMiddleware>
    {
        public int ErrorCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error && exception is not null)
            {
                ErrorCount++;
            }
        }
    }

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }
}
