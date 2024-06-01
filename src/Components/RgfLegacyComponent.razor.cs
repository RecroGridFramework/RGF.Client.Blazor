using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Services;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfLegacyComponent : ComponentBase, IAsyncDisposable
{
    [Inject]
    IServiceProvider _serviceProvider { get; set; } = default!;

    [Inject]
    IRecroSecService _recroSec { get; set; } = default!;

    [Inject]
    IJSRuntime _jsRuntime { get; set; } = default!;

    private static string _sessionId = Guid.NewGuid().ToString("N");

    private readonly string _containerId = $"rgf-legacy-{Guid.NewGuid():N}";

    private DotNetObjectReference<RgfLegacyComponent>? _selfRef { get; set; }

    private string? _entityName { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadResourcesAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (_entityName != EntityParameters.EntityName)
        {
            _entityName = EntityParameters.EntityName;
            await _jsRuntime.InvokeVoidAsync("eval", $"$('#{_containerId}').html('');");
            _selfRef?.Dispose();
            _selfRef = DotNetObjectReference.Create(this);
            if (EntityParameters.EntityName == "RecroSec")
            {
                await _jsRuntime.InvokeVoidAsync($"{JsRgfLegacyNamespace}.CreateRecroSecAsync", _containerId, _selfRef);
            }
            else if (EntityParameters.EntityName.Equals("RgfMenu", StringComparison.OrdinalIgnoreCase))
            {
                object[][] filter = [[RgfFilter.LogicalOperator.And.ToString().ToLower(), RgfFilter.QueryOperator.Equal, "rg-col-951", 10]];
                await _jsRuntime.InvokeVoidAsync($"{JsRgfLegacyNamespace}.CreateRecroGridAsync", "RGF_Menu_1", _containerId, _selfRef, filter);
            }
            else
            {
                await _jsRuntime.InvokeVoidAsync($"{JsRgfLegacyNamespace}.CreateRecroGridAsync", EntityParameters.EntityName, _containerId, _selfRef);
            }
        }
    }

    private async Task LoadResourcesAsync()
    {
        var libName = Assembly.GetExecutingAssembly().GetName().Name;
        if (SriptReferences.Length == 0 || StylesheetsReferences.Length == 0)
        {
            var jquery = await _jsRuntime.InvokeAsync<bool>("eval", "window.jQuery?.ui?.version == '1.13.2'");
            if (!jquery)
            {
                await _jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/lib/jqueryui/jquery-ui.min.js");
            }

            var api = _serviceProvider.GetRequiredService<IRgfApiService>();
            var res = await api.GetAsync<string[]>("/rgf/api/RGFSriptReferences/-legacy-blazor-", authClient: false);
            if (res.Success)
            {
                SriptReferences = res.Result.Where(e => !RgfBlazorConfigurationExtension.SriptReferences.Contains(e)).ToArray();
            }
            res = await api.GetAsync<string[]>("/rgf/api/RGFStylesheetsReferences", authClient: false);
            if (res.Success)
            {
                StylesheetsReferences = res.Result;
            }
            foreach (var item in SriptReferences)
            {
                //await _jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddScriptLinkAsync", ApiService.BaseAddress + item, null, "rgf-legacy");
                await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ApiService.BaseAddress + item);
            }
            await _jsRuntime.InvokeAsync<IJSObjectReference>("import", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/scripts/" +
#if DEBUG
                "recrovit-rgf-blazor-legacy.js"
#else
                "recrovit-rgf-blazor-legacy.min.js"
#endif
                );
        }
        await _jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.AddStyleSheetLink", $"{RgfClientConfiguration.AppRootPath}_content/{libName}/lib/jqueryui/themes/base/jquery-ui.min.css", false, null, null, "rgf-legacy");
        foreach (var item in StylesheetsReferences)
        {
            await _jsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", ApiService.BaseAddress + item, false, null, null, "rgf-legacy");
        }
    }

    private async Task UnloadResourcesAsync()
    {
        await _jsRuntime.InvokeVoidAsync("eval", "$('link[data-component=\"rgf-legacy\"]').remove();");
    }

    [JSInvokable]
    public async Task<string> GetAuthorizationHeaderAsync()
    {
        var token = await _recroSec.GetAccessTokenAsync();
        return string.IsNullOrEmpty(token) ? "" : $"Bearer {token}";
    }

    [JSInvokable]
    public string GetSessionId() => _sessionId;

    private static string[] SriptReferences { get; set; } = [];

    private static string[] StylesheetsReferences { get; set; } = [];

    internal static readonly string JsRgfLegacyNamespace = "Recrovit.RGF.Blazor.Legacy";

    public async ValueTask DisposeAsync()
    {
        _selfRef?.Dispose();
        await UnloadResourcesAsync();
    }
}