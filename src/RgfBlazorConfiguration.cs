using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.Blazor.Handlers;
using Recrovit.RecroGridFramework.Client.Services;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor;

public class RgfBlazorConfiguration
{
    internal static Dictionary<string, Type> EntityComponentTypes { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal static Dictionary<ComponentType, Type> ComponentTypes { get; } = new();

    public static void RegisterEntityComponent<TComponent>(string entityName) where TComponent : ComponentBase
    {
        EntityComponentTypes[entityName ?? string.Empty] = typeof(TComponent);
    }

    public static void ClearEntityComponentTypes() => EntityComponentTypes.Clear();

    public static void RegisterComponent<TComponent>(ComponentType type) where TComponent : ComponentBase
    {
        ComponentTypes[type] = typeof(TComponent);
    }

    public enum ComponentType
    {
        Menu,
        Dialog
    }

    public static readonly string JsBlazorNamespace = "Recrovit.RGF.Blazor.Client";
}

public static class RgfBlazorConfigurationExtension
{
    public static IServiceCollection AddRgfBlazorServices(this IServiceCollection services, IConfiguration configuration, ILogger? logger = null, Type? authorizationMessageHandlerType = null)
    {
        services.AddRgfServices(configuration, logger);

        var httpClientBuilder = services.AddHttpClient(ApiService.RgfAuthApiClientName, httpClient => httpClient.BaseAddress = new Uri(ApiService.BaseAddress));

        var config = configuration.GetSection("Recrovit:RecroGridFramework");
        if (config.GetSection("API:DefaultScopes").Get<string[]>() != null)
        {
            if (authorizationMessageHandlerType == null || !typeof(DelegatingHandler).IsAssignableFrom(authorizationMessageHandlerType))
            {
                services.AddTransient<RgfAuthorizationMessageHandler>();
                authorizationMessageHandlerType = typeof(RgfAuthorizationMessageHandler);
            }
            logger?.LogInformation($"Initializing AuthorizationMessageHandler for RecroGrid Framework API with type '{authorizationMessageHandlerType.Name}'.");
            httpClientBuilder.Services.Configure<HttpClientFactoryOptions>(httpClientBuilder.Name, options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(b => b.AdditionalHandlers.Add((DelegatingHandler)b.Services.GetRequiredService(authorizationMessageHandlerType)));
            });
        }
        return services;
    }

    public static Task InitializeRgfBlazorServerAsync(this IServiceProvider serviceProvider) => serviceProvider.InitializeRgfBlazorAsync(false);

    public static async Task InitializeRgfBlazorAsync(this IServiceProvider serviceProvider, bool clientSideRendering = true)
    {
        await serviceProvider.InitializeRgfClientAsync(clientSideRendering);
        if (clientSideRendering)
        {
            await LoadResourcesAsync(serviceProvider);
        }
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        var logger = serviceProvider.GetRequiredService<ILogger<RgfBlazorConfiguration>>();
        logger?.LogInformation("RecroGrid Framework Blazor v{version} initialized.", version);
    }

    public static async Task LoadResourcesAsync(IServiceProvider serviceProvider)
    {
        var jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();
        var api = serviceProvider.GetRequiredService<IRgfApiService>();
        var libName = Assembly.GetExecutingAssembly().GetName().Name;

        bool jquery = await jsRuntime.InvokeAsync<bool>("eval", "typeof jQuery != 'undefined'");
        if (!jquery)
        {
            await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/lib/jquery/jquery.min.js");
        }

        var res = await api.GetAsync<string[]>("/rgf/api/RGFSriptReferences", authClient: false);
        if (res.Success)
        {
            SriptReferences = res.Result;
            foreach (var item in SriptReferences)
            {
                await jsRuntime.InvokeAsync<IJSObjectReference>("import", ApiService.BaseAddress + item);
            }
            await jsRuntime.InvokeVoidAsync($"Recrovit.WebCli.SetBaseAddress", ApiService.BaseAddress);
            await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", $"{ApiService.BaseAddress}/rgf/resource/RgfCore.css");
        }
        await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/scripts/" +
#if DEBUG
            "recrovit-rgf-blazor.js"
#else
            "recrovit-rgf-blazor.min.js"
#endif
            );
    }

    internal static string[] SriptReferences { get; set; } = [];
}