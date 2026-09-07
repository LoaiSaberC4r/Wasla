using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildingBlock.Tests;

internal sealed class BuildingBlockTestApplication : IAsyncDisposable
{
    private readonly WebApplication _application;

    private BuildingBlockTestApplication(WebApplication application, HttpClient client)
    {
        _application = application;
        Client = client;
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => _application.Services;

    public static async Task<BuildingBlockTestApplication> CreateAsync(
        Action<IServiceCollection>? configureServices = null,
        Action<WebApplication>? configureApplication = null,
        string environmentName = "Development")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(BuildingBlockTestApplication).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = environmentName
        });

        builder.WebHost.UseTestServer();
        configureServices?.Invoke(builder.Services);

        var application = builder.Build();
        configureApplication?.Invoke(application);
        await application.StartAsync();

        return new BuildingBlockTestApplication(application, application.GetTestClient());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
    }
}
