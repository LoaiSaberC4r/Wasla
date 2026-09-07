using BuildingBlock.Api.Bootstrap;
using BuildingBlock.Api.Localization.Providers;
using BuildingBlock.Api.Middleware;
using BuildingBlock.Api.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace BuildingBlock.Tests;

public sealed class LocalizationOptionsTests
{
    [Fact]
    public void Default_configuration_is_valid()
    {
        Assert.True(SharedLocalizationOptionsValidator.IsValid(new SharedLocalizationOptions()));
    }

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Invalid_configuration_fails_validation(SharedLocalizationOptions options)
    {
        Assert.False(SharedLocalizationOptionsValidator.IsValid(options));
    }

    [Fact]
    public async Task Accept_language_exact_neutral_quality_and_wildcard_behaviors()
    {
        var provider = new SimpleAcceptLanguageProvider(new SharedLocalizationOptions
        {
            SupportedCultures = new[] { "ar", "en", "fr" },
            DefaultCulture = "en"
        });

        Assert.Equal("fr", (await Determine(provider, "fr;q=0.9,en;q=0.8"))!.Cultures[0].Value);
        Assert.Equal("ar", (await Determine(provider, "ar-EG,en;q=0.9"))!.Cultures[0].Value);
        Assert.Equal("en", (await Determine(provider, "ar;q=0,*;q=0.5"))!.Cultures[0].Value);
        Assert.Null(await Determine(provider, "fr;q=bad,en;q=0"));
    }

    [Fact]
    public async Task Query_string_provider_returns_only_supported_exact_cultures()
    {
        var provider = new SupportedQueryStringRequestCultureProvider(
            Options.Create(new SharedLocalizationOptions
            {
                SupportedCultures = new[] { "ar", "en" },
                DefaultCulture = "en"
            }));

        var supported = new DefaultHttpContext();
        supported.Request.QueryString = new QueryString("?lang=ar");
        var unsupported = new DefaultHttpContext();
        unsupported.Request.QueryString = new QueryString("?lang=fr");

        Assert.Equal("ar", (await provider.DetermineProviderCultureResult(supported))!.Cultures[0].Value);
        Assert.Null(await provider.DetermineProviderCultureResult(unsupported));
    }

    [Fact]
    public void Swagger_and_runtime_use_same_supported_culture_options()
    {
        var services = new ServiceCollection();
        services.AddBuildingBlockLocalization(options =>
        {
            options.SupportedCultures = new[] { "en", "fr" };
            options.DefaultCulture = "fr";
        });

        using var provider = services.BuildServiceProvider();

        var shared = provider.GetRequiredService<IOptions<SharedLocalizationOptions>>().Value;
        var runtime = provider.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
        Assert.Equal(shared.SupportedCultures, runtime.SupportedCultures!.Select(culture => culture.Name));
        Assert.Equal("fr", runtime.DefaultRequestCulture.Culture.Name);
    }

    [Fact]
    public async Task Content_language_matches_current_ui_culture()
    {
        await using var application = await BuildingBlockTestApplication.CreateAsync(
            services => services.AddBuildingBlockLocalization(options =>
            {
                options.SupportedCultures = new[] { "ar", "en" };
                options.DefaultCulture = "en";
            }),
            app =>
            {
                app.UseBuildingBlockLocalization();
                app.Run(context => context.Response.WriteAsync("ok"));
            });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.AcceptLanguage.ParseAdd("ar");

        using var response = await application.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("ar", response.Content.Headers.ContentLanguage.Single());
    }

    public static IEnumerable<object[]> InvalidOptions()
    {
        yield return new object[] { new SharedLocalizationOptions { SupportedCultures = Array.Empty<string>() } };
        yield return new object[] { new SharedLocalizationOptions { SupportedCultures = new[] { "not-a-culture" } } };
        yield return new object[] { new SharedLocalizationOptions { SupportedCultures = new[] { "en" }, DefaultCulture = "fr" } };
        yield return new object[] { new SharedLocalizationOptions { SupportedCultures = new[] { "en", "EN" }, DefaultCulture = "en" } };
    }

    private static Task<ProviderCultureResult?> Determine(SimpleAcceptLanguageProvider provider, string header)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = header;
        return provider.DetermineProviderCultureResult(context);
    }
}
