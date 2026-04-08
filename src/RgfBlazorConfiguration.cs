using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.Blazor.Handlers;
using Recrovit.RecroGridFramework.Client.Blazor.Services;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Services;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor;

public class RgfBlazorConfiguration
{
    internal static Dictionary<string, Type> EntityComponentTypes { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal static Dictionary<ComponentType, Type> ComponentTypes { get; } = [];

    public static void RegisterEntityComponent<TComponent>(string entityName) where TComponent : ComponentBase
    {
        EntityComponentTypes[entityName ?? string.Empty] = typeof(TComponent);
    }

    public static void ClearEntityComponentTypes() => EntityComponentTypes.Clear();

    public static void RegisterComponent<TComponent>(ComponentType type) where TComponent : ComponentBase
    {
        ComponentTypes[type] = typeof(TComponent);
    }

    public static void UnregisterComponent(ComponentType type) => ComponentTypes.Remove(type);

    public static Type GetComponentType(ComponentType type)
    {
        if (!ComponentTypes.TryGetValue(type, out Type? componentType))
        {
            throw new NotImplementedException($"The {type} template component is missing.");
        }
        return componentType;
    }

    public static bool TryGetComponentType(ComponentType type, out Type? componentType) => ComponentTypes.TryGetValue(type, out componentType);

    public enum ComponentType
    {
        Menu = 1,
        Dialog = 2,
        Chart = 3,
    }

    public const string JsBlazorNamespace = "Recrovit.RGF.Blazor.Client";

    public const string JQueryUiVer = "1.14.1";

    public static ValueTask<int> ChkJQueryUiVer(IJSRuntime jsRuntime) => jsRuntime.InvokeAsync<int>("Recrovit.LPUtils.CompareJQueryUIVersion", JQueryUiVer);

    public static string Version => _version.Value;

    private static readonly Lazy<string> _version = new Lazy<string>(() => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);

    internal static readonly Version MinimumRgfCoreVersion = new Version(10, 0, 0);//RGF.Core MinVersion
}

public static class RgfBlazorConfigurationExtension
{
    private static readonly string _executingAssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

    [Obsolete("Use AddRgfBlazorWasmBearerServices for Blazor WebAssembly with bearer tokens.")]
    public static IServiceCollection AddRgfBlazorServices(this IServiceCollection services, IConfiguration configuration, ILogger? logger = null, Type? authorizationMessageHandlerType = null) =>
        services.AddRgfBlazorWasmBearerServices(configuration, logger, authorizationMessageHandlerType);

    public static IServiceCollection AddRgfBlazorWasmBearerServices(this IServiceCollection services, IConfiguration configuration, ILogger? logger = null, Type? authorizationMessageHandlerType = null)
    {
        AddRgfBlazorServicesCore(services, configuration, logger, RgfApiAuthMode.WasmBearer);
        services.AddScoped<IRgfAccessTokenAccessor, WasmRgfAccessTokenAccessor>();
        ConfigureWasmAuthHttpClient(services, authorizationMessageHandlerType, logger);
        return services;
    }

    private static void ConfigureWasmAuthHttpClient(IServiceCollection services, Type? authorizationMessageHandlerType, ILogger? logger)
    {
        if (authorizationMessageHandlerType == null || !typeof(DelegatingHandler).IsAssignableFrom(authorizationMessageHandlerType))
        {
            services.AddTransient<RgfAuthorizationMessageHandler>();
            authorizationMessageHandlerType = typeof(RgfAuthorizationMessageHandler);
        }

        logger?.LogInformation("RecroGrid Framework Blazor registration: WebAssembly bearer auth with handler '{AuthorizationMessageHandlerTypeName}'.", authorizationMessageHandlerType.Name);

        services.Configure<HttpClientFactoryOptions>(ApiService.RgfAuthApiClientName, httpClientOptions =>
        {
            httpClientOptions.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.AdditionalHandlers.Add((DelegatingHandler)builder.Services.GetRequiredService(authorizationMessageHandlerType));
            });
        });
    }

    public static IServiceCollection AddRgfBlazorWithoutAuthServices(this IServiceCollection services, IConfiguration configuration, ILogger? logger = null)
    {
        AddRgfBlazorServicesCore(services, configuration, logger, RgfApiAuthMode.None);
        logger?.LogInformation("RecroGrid Framework Blazor registration: without built-in authentication handling.");
        return services;
    }

    public static IServiceCollection AddRgfBlazorServerProxyClientServices(this IServiceCollection services, IConfiguration configuration, string browserBaseAddress, ILogger? logger = null)
    {
        AddRgfBlazorServicesCore(services, configuration, logger, RgfApiAuthMode.ServerProxy, browserBaseAddress);
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddAuthenticationStateDeserialization();
        services.AddSingleton<IRgfAuthenticationSessionMonitor, RgfAuthenticationSessionMonitor>();
        services.AddSingleton<RgfAuthenticationPrincipalSnapshotSynchronizer>();
        services.AddSingleton<IRgfAuthenticationFailureHandler>(serviceProvider => serviceProvider.GetRequiredService<IRgfAuthenticationSessionMonitor>());
        services.DecorateAuthenticationStateProvider();
        logger?.LogInformation("RecroGrid Framework Blazor registration: client server-proxy auth via host '{BrowserBaseAddress}'.", browserBaseAddress);
        return services;
    }

    public static IServiceCollection AddRgfBlazorServerProxySsrServices(this IServiceCollection services, IConfiguration configuration, string browserBaseAddress, ILogger? logger = null)
    {
        services.TryAddScoped<IRgfServerRequestCookieAccessor, NoOpRgfServerRequestCookieAccessor>();
        services.AddTransient<RgfServerProxyAuthCookieHandler>();
        services.TryAddSingleton<IRgfAuthenticationSessionMonitor, NoOpRgfAuthenticationSessionMonitor>();

        AddRgfBlazorServicesCore(services, configuration, logger, RgfApiAuthMode.ServerProxySsr, browserBaseAddress);
        ConfigureServerProxySsrHttpClients(services, logger);
        logger?.LogInformation("RecroGrid Framework Blazor registration: SSR server-proxy auth via host '{BrowserBaseAddress}'.", browserBaseAddress);
        return services;
    }

    private static IServiceCollection AddRgfBlazorServicesCore(IServiceCollection services, IConfiguration configuration, ILogger? logger,
        RgfApiAuthMode authMode, string? browserBaseAddress = null)
    {
        services.AddRgfServices(configuration, logger, authMode, browserBaseAddress);
        services.TryAddSingleton<RgfAuthenticationEndpointResolver>();

        if (RgfClientConfiguration.ClientVersions.TryAdd(RgfHeaderKeys.RgfClientBlazorVersion, RgfBlazorConfiguration.Version))
        {
            RgfClientConfiguration.ClientVersions.Remove(RgfHeaderKeys.RgfClientVersion);
        }

        if (RgfClientConfiguration.MinimumRgfCoreVersion < RgfBlazorConfiguration.MinimumRgfCoreVersion)
        {
            RgfClientConfiguration.MinimumRgfCoreVersion = RgfBlazorConfiguration.MinimumRgfCoreVersion;
        }

        return services;
    }

    private static void ConfigureServerProxySsrHttpClients(IServiceCollection services, ILogger? logger)
    {
        logger?.LogInformation("RecroGrid Framework Blazor registration: SSR server-proxy handler '{AuthorizationMessageHandlerTypeName}' attached to RGF HTTP clients.", nameof(RgfServerProxyAuthCookieHandler));

        foreach (var clientName in new[] { ApiService.RgfApiClientName, ApiService.RgfAuthApiClientName })
        {
            services.Configure<HttpClientFactoryOptions>(clientName, httpClientOptions =>
            {
                httpClientOptions.HttpMessageHandlerBuilderActions.Add(builder =>
                {
                    builder.AdditionalHandlers.Add(builder.Services.GetRequiredService<RgfServerProxyAuthCookieHandler>());
                });
            });
        }
    }

    public static Task InitializeRgfBlazorServerAsync(this IServiceProvider serviceProvider) => serviceProvider.InitializeRgfBlazorAsync(false);

    public static async Task InitializeRgfBlazorAsync(this IServiceProvider serviceProvider, bool clientSideRendering = true)
    {
        await serviceProvider.InitializeRgfClientAsync(clientSideRendering);
        if (clientSideRendering)
        {
            var authenticationSessionMonitor = serviceProvider.GetService<IRgfAuthenticationSessionMonitor>();
            if (authenticationSessionMonitor != null)
            {
                await authenticationSessionMonitor.ProbeAsync(CancellationToken.None);
            }
        }
        if (clientSideRendering)
        {
            await LoadResourcesAsync(serviceProvider);
        }
        var logger = serviceProvider.GetRequiredService<ILogger<RgfBlazorConfiguration>>();
        logger?.LogInformation("RecroGrid Framework Blazor v{version} initialized.", RgfBlazorConfiguration.Version);
    }

    public static async Task LoadResourcesAsync(IServiceProvider serviceProvider)
    {
        var jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();

        bool jquery = await jsRuntime.InvokeAsync<bool>("eval", "typeof jQuery != 'undefined'");
        if (!jquery)
        {
            await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}/_content/{_executingAssemblyName}/lib/jquery/jquery.min.js");
        }

        if (!SriptReferences.Any())
        {
            var api = serviceProvider.GetRequiredService<IRgfApiService>();
            var res = await api.GetAsync<string[]>("/rgf/api/RGFSriptReferences", authClient: false);
            if (res.Success)
            {
                SriptReferences = res.Result;
                foreach (var item in SriptReferences)
                {
                    await jsRuntime.InvokeAsync<IJSObjectReference>("import", ApiService.BaseAddress + item);
                }
                await jsRuntime.InvokeVoidAsync($"Recrovit.WebCli.SetBaseAddress", ApiService.BaseAddress);
            }
            await jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}/_content/{_executingAssemblyName}/scripts/" +
#if DEBUG
                "recrovit-rgf-blazor.js"
#else
            "recrovit-rgf-blazor.min.js"
#endif
                );

            await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", GetRgfCoreCssHref(), false, RgfCoreCssId);
        }

        await jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.EnsureStyleSheetLoaded", "rgf-check-stylesheet-client-blazor", "<div class=\"rgf-check-stylesheet-client-blazor\" rgf-wrapper-comp=\"\">",
            GetBundleCssHref(), BlazorCssLib);
    }

    public const string RgfCoreCssId = "rgf-core-css";

    public const string BlazorCssLib = "rgf-client-blazor-lib";

    public static string GetRgfCoreCssHref() => $"{ApiService.BaseAddress}/rgf/resource/RgfCore.css";

    public static string GetBundleCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_executingAssemblyName}/{_executingAssemblyName}.bundle.scp.css?v={RgfBlazorConfiguration.Version}";

    public static string GetJQueryUiCssHref() => $"{RgfClientConfiguration.AppRootPath}/_content/{_executingAssemblyName}/lib/jqueryui/themes/base/jquery-ui.min.css";

    public static IEnumerable<string> SriptReferences { get; private set; } = [];
}
