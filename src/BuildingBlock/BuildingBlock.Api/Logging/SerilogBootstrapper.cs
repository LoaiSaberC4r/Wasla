using BuildingBlock.Api.Middleware;
using BuildingBlock.Api.Options;
using BuildingBlock.Api.ProblemDetails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace BuildingBlock.Api.Logging
{
    public static class SerilogBootstrapper
    {
        private static readonly LoggingLevelSwitch GlobalLevel = new(LogEventLevel.Information);

        public static WebApplicationBuilder AddBuildingBlockSerilog(this WebApplicationBuilder builder, string domainArea)
        {
            var contentRoot = builder.Environment.ContentRootPath;
            var logsRoot = Path.Combine(contentRoot, "logs");
            Directory.CreateDirectory(logsRoot);

            SelfLog.Enable(message =>
            {
                try
                {
                    var line = $"[{DateTime.UtcNow:O}] {message}";
                    Console.Error.WriteLine("SERILOG-SELFLOG: " + line);
                    File.AppendAllText(Path.Combine(logsRoot, "serilog-selflog.txt"), line + Environment.NewLine);
                }
                catch
                {
                    // Serilog self-log should never break application startup.
                }
            });

            var loggingOptions = new LoggingOptions();
            builder.Configuration.GetSection("Logging").Bind(loggingOptions);
            var redactionOptions = new LogRedactionOptions();
            builder.Configuration.GetSection("Logging:Redaction").Bind(redactionOptions);
            var redactionPolicy = new DefaultLogRedactionPolicy(redactionOptions);

            builder.Services.AddBuildingBlockLogRedaction(builder.Configuration.GetSection("Logging:Redaction"));
            builder.Services.AddOptions<RequestLoggingOptions>()
                .Bind(builder.Configuration.GetSection("Logging:Requests"))
                .Validate(ValidateRequestLoggingOptions, "Request logging options are invalid.")
                .ValidateOnStart();

            TryEnsureDirectoryFromConfigPath(
                builder.Configuration,
                "Serilog:WriteTo:1:Args:configure:1:Args:bufferBaseFilename",
                contentRoot);
            TryEnsureDirectoryFromConfigPath(
                builder.Configuration,
                "Serilog:WriteTo:0:Args:configure:1:Args:bufferBaseFilename",
                contentRoot);
            TryEnsureDirectoryFromConfigPath(
                builder.Configuration,
                "Serilog:WriteTo:1:Args:configure:2:Args:path",
                contentRoot);
            TryEnsureDirectoryFromConfigPath(
                builder.Configuration,
                "Serilog:WriteTo:0:Args:configure:2:Args:path",
                contentRoot);

            var loggerConfiguration = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .MinimumLevel.ControlledBy(GlobalLevel)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.With(new DomainAreaEnricher(domainArea))
                .Enrich.With(new RedactionEnricher(redactionPolicy));

            if (loggingOptions.Sampling?.Enabled == true && loggingOptions.Sampling.KeepRate is > 0 and < 1)
            {
                var maxLevel = ParseLevelOrDefault(loggingOptions.Sampling.MaxLevelToSample, LogEventLevel.Information);
                loggerConfiguration = loggerConfiguration.Filter.With(new SamplingFilter(loggingOptions.Sampling.KeepRate, maxLevel));
            }

            Log.Logger = loggerConfiguration.CreateLogger();
            builder.Host.UseSerilog();

            builder.Services.AddSingleton(GlobalLevel);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddBuildingBlockProblemDetails();

            return builder;
        }

        public static IApplicationBuilder UseBuildingBlockSerilog(this IApplicationBuilder app)
        {
            var requestLoggingOptions = app.ApplicationServices
                .GetRequiredService<IOptions<RequestLoggingOptions>>()
                .Value;
            var redactionPolicy = app.ApplicationServices.GetRequiredService<ILogRedactionPolicy>();
            var sanitizer = new RequestLogSanitizer(redactionPolicy, requestLoggingOptions);

            app.UseMiddleware<CorrelationIdMiddleware>();

            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate =
                    "HTTP {RequestMethod} {RequestPath} => {StatusCode} in {Elapsed:0.0000} ms (corr={CorrelationId})";
                options.GetLevel = (httpContext, _, exception) =>
                    sanitizer.IsExcluded(httpContext.Request.Path) && exception is null
                        ? LogEventLevel.Verbose
                        : exception is null
                            ? LogEventLevel.Information
                            : LogEventLevel.Error;
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestPath", sanitizer.NormalizePath(httpContext.Request.Path));
                    diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
                    if (requestLoggingOptions.IncludeClientIp)
                    {
                        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
                    }

                    if (requestLoggingOptions.IncludeUserAgent)
                    {
                        diagnosticContext.Set(
                            "UserAgent",
                            sanitizer.RedactText("UserAgent", httpContext.Request.Headers.UserAgent.ToString()));
                    }

                    if (requestLoggingOptions.IncludeQueryString)
                    {
                        diagnosticContext.Set("RequestQuery", sanitizer.SanitizeQuery(httpContext.Request.QueryString));
                    }

                    if (requestLoggingOptions.IncludeRequestHeaders)
                    {
                        diagnosticContext.Set(
                            "RequestHeaders",
                            sanitizer.SanitizeHeaders(
                                httpContext.Request.Headers,
                                requestLoggingOptions.AllowedRequestHeaders));
                    }

                    if (requestLoggingOptions.IncludeResponseHeaders)
                    {
                        diagnosticContext.Set(
                            "ResponseHeaders",
                            sanitizer.SanitizeHeaders(
                                httpContext.Response.Headers,
                                requestLoggingOptions.AllowedResponseHeaders));
                    }

                    if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.CorrelationItemKey, out var correlationId))
                    {
                        diagnosticContext.Set("CorrelationId", correlationId?.ToString());
                    }
                };
            });

            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.UseBuildingBlockProblemDetailsStatusCodes();

            return app;
        }

        public static IServiceCollection AddBuildingBlockLogRedaction(
            this IServiceCollection services,
            IConfiguration? configuration = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var optionsBuilder = services.AddOptions<LogRedactionOptions>();
            if (configuration is not null)
            {
                optionsBuilder.Bind(configuration);
            }

            optionsBuilder
                .Validate(DefaultLogRedactionPolicy.IsValid, "Log redaction options are invalid.")
                .ValidateOnStart();

            services.AddSingleton(provider =>
                new DefaultLogRedactionPolicy(provider.GetRequiredService<IOptions<LogRedactionOptions>>()));
            services.AddSingleton<ILogRedactionPolicy>(provider =>
                provider.GetRequiredService<DefaultLogRedactionPolicy>());

            return services;
        }

        public static IEndpointRouteBuilder MapBuildingBlockLoggingDiagnostics(
            this IEndpointRouteBuilder endpoints,
            string authorizationPolicy = "BuildingBlockDiagnostics")
        {
            var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
            var configuration = endpoints.ServiceProvider.GetRequiredService<IConfiguration>();
            var enabled = bool.TryParse(configuration["Logging:Diagnostics:Enabled"], out var configured)
                ? configured
                : !environment.IsProduction();

            if (!enabled)
            {
                return endpoints;
            }

            var group = endpoints.MapGroup("/internal/logging")
                .RequireAuthorization(new AuthorizeAttribute { Policy = authorizationPolicy });

            group.MapGet("/diag", () => Results.Ok(new
            {
                diagnosticsEnabled = true,
                environment = environment.EnvironmentName,
                slo = new { latency99_ms = 1000, maxQueue = 5000 }
            }));

            group.MapPost("/level", (string level) =>
            {
                if (!Enum.TryParse<LogEventLevel>(level, true, out var parsed))
                {
                    return Results.BadRequest(new
                    {
                        error = "Invalid level. Use: Verbose|Debug|Information|Warning|Error|Fatal"
                    });
                }

                GlobalLevel.MinimumLevel = parsed;
                Log.Warning("Logging level switched to {Level}", parsed);
                return Results.Ok(new { level = parsed.ToString() });
            });

            return endpoints;
        }

        private static LogEventLevel ParseLevelOrDefault(string? level, LogEventLevel fallback)
            => Enum.TryParse<LogEventLevel>(level, true, out var parsed) ? parsed : fallback;

        private static void TryEnsureDirectoryFromConfigPath(ConfigurationManager configuration, string key, string contentRoot)
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var fullPath = Path.GetFullPath(value, contentRoot);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static bool ValidateRequestLoggingOptions(RequestLoggingOptions options)
            => options.MaximumValueLength > "...[truncated]".Length &&
               options.ExcludedPaths.All(path =>
                   !string.IsNullOrWhiteSpace(path) &&
                   path.StartsWith('/')) &&
               options.AllowedRequestHeaders.All(header => !string.IsNullOrWhiteSpace(header)) &&
               options.AllowedResponseHeaders.All(header => !string.IsNullOrWhiteSpace(header));
    }
}
