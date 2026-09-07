using BuildingBlock.Api.Localization.Providers;
using BuildingBlock.Api.Logging;
using BuildingBlock.Api.Middleware;
using BuildingBlock.Api.Options;
using Microsoft.AspNetCore.Http;
using Serilog.Events;
using Serilog.Parsing;

namespace BuildingBlock.Tests;

public sealed class SecurityLoggingLocalizationTests
{
    [Fact]
    public void Bearer_token_is_fully_redacted()
    {
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            new MessageTemplateParser().Parse("{Header}"),
            new[] { new LogEventProperty("Header", new ScalarValue("Authorization: Bearer abc.def.ghi")) });

        new RedactionEnricher().Enrich(logEvent, new TestPropertyFactory());

        var value = Assert.IsType<ScalarValue>(logEvent.Properties["Header"]).Value as string;
        Assert.Equal("Authorization: Bearer ***", value);
    }

    [Fact]
    public async Task Invalid_correlation_ids_are_replaced()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationHeader] = "bad\r\nvalue";
        context.Response.Body = new MemoryStream();

        var middleware = new CorrelationIdMiddleware(next => next.Response.WriteAsync("ok"));
        await middleware.Invoke(context);

        var correlationId = Assert.IsType<string>(context.Items[CorrelationIdMiddleware.CorrelationItemKey]);
        Assert.Matches("^[a-f0-9]{32}$", correlationId);
    }

    [Fact]
    public async Task Accept_language_provider_supports_neutral_fallback()
    {
        var provider = new SimpleAcceptLanguageProvider(new SharedLocalizationOptions
        {
            SupportedCultures = new[] { "ar", "en" },
            DefaultCulture = "en"
        });

        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = "ar-EG, en-US;q=0.9";

        var result = await provider.DetermineProviderCultureResult(context);

        Assert.Equal("ar", result!.Cultures[0].Value);
    }

    private sealed class TestPropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
            => new(name, new ScalarValue(value));
    }
}
