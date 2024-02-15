using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Models;
using System.Globalization;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfGridColumnComponent : ComponentBase
{
    [Inject]
    private IJSRuntime _jsRuntime { get; set; } = null!;

    private ElementReference _elementRef;

    private RgfEntity EntityDesc => GridColumnParameters.BaseGridComponent.Manager.EntityDesc;

    private IRecroSecService RecroSec => GridColumnParameters.BaseGridComponent.Manager.RecroSec;

    private RgfProperty PropDesc => GridColumnParameters.PropDesc;

    private RgfDynamicDictionary RowData => GridColumnParameters.RowData;

    private string? Data { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        var objData = RowData?.GetMember(PropDesc.Alias);
        Data = objData?.ToString() ?? "";
        object? value;
        if (PropDesc.Options?.TryGetValue("RGO_JSReplace", out value) == true)
        {
            Data = await _jsRuntime.InvokeAsync<string>(RgfBlazorConfiguration.JsBlazorNamespace + ".invokeGridColFuncAsync", value.ToString(), CreateJSArgs(EntityDesc, RowData, PropDesc, Data));
        }
        else if (objData is DateTime && PropDesc.ListType == PropertyListType.Date)
        {
            CultureInfo culture = RecroSec.UserCultureInfo();
            if (PropDesc.FormType == PropertyFormType.DateTime)
            {
                Data = string.Format("{0} {1}",
                    ((DateTime)objData).ToString("d", culture).Replace(" ", ""),
                    ((DateTime)objData).ToString("HH:mm:ss", culture).Replace(" ", ""));
            }
            else
            {
                Data = ((DateTime)objData).ToString("d", culture).Replace(" ", "");
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JSHandlerAsync();
        await base.OnAfterRenderAsync(firstRender);
    }

    protected virtual async Task JSHandlerAsync()
    {
        object? value;
        if (PropDesc.Options?.TryGetValue("RGO_JQReplace", out value) == true)
        {
            await _jsRuntime.InvokeVoidAsync(RgfBlazorConfiguration.JsBlazorNamespace + ".invokeGridColActionAsync", value.ToString(), CreateJSArgs(EntityDesc, RowData, PropDesc, Data, _elementRef));
        }
    }

    public static Dictionary<string, object> CreateJSArgs(RgfEntity entityDesc, RgfDynamicDictionary? rowData, RgfProperty? propDesc = null, string? data = null, ElementReference? elementRef = null)
    {
        var obj = new Dictionary<string, object>();

        var columns = new Dictionary<string, object>();
        foreach (var item in entityDesc.Properties)
        {
            columns.Add(item.ClientName, new
            {
                Value = rowData?.GetMember(item.Alias),
                ColumnDefs = item
            });
        }
        obj.Add("Columns", columns);

        if (elementRef != null)
        {
            obj.Add("Self", elementRef);
        }
        if (propDesc != null)
        {
            obj.Add("Value", data ?? (rowData?.GetMember(propDesc.Alias) ?? ""));
        }
        return obj;
    }

    #region Row/Cell Style
    public static Task<List<string>> GetRowClassAsync(IJSRuntime jsRuntime, RgfEntity entityDesc, RgfDynamicDictionary rowData) => GetRowStyleAsync("RGO_JSRowClass", jsRuntime, entityDesc, rowData);
    public static Task<List<string>> GetRowStyleAsync(IJSRuntime jsRuntime, RgfEntity entityDesc, RgfDynamicDictionary rowData) => GetRowStyleAsync("RGO_JSRowStyle", jsRuntime, entityDesc, rowData);
    private static async Task<List<string>> GetRowStyleAsync(string key, IJSRuntime jsRuntime, RgfEntity entityDesc, RgfDynamicDictionary rowData)
    {
        var list = new List<string>();
        char separator = key == "RGO_JSRowClass" ? ' ' : ';';
        if (entityDesc.Options?.TryGetValue(key, out object? val) == true)
        {
            var jsArgs = RgfGridColumnComponent.CreateJSArgs(entityDesc, rowData);
            var css = await jsRuntime.InvokeAsync<string>(RgfBlazorConfiguration.JsBlazorNamespace + ".invokeGridColFuncAsync", val, jsArgs);
            if (!string.IsNullOrWhiteSpace(css))
            {
                list.AddRange(css.Trim().Split(separator));
            }
        }

        foreach (var prop in entityDesc.Properties.Where(e => e.Options?.Any(o => o.Key == key) == true))
        {
            var jsArgs = RgfGridColumnComponent.CreateJSArgs(entityDesc, rowData, prop);
            var fn = prop.Options.GetStringValue(key);
            var css = await jsRuntime.InvokeAsync<string>(RgfBlazorConfiguration.JsBlazorNamespace + ".invokeGridColFuncAsync", fn, jsArgs);
            if (!string.IsNullOrWhiteSpace(css))
            {
                list.AddRange(css.Trim().Split(separator));
            }
        }
        return list;
    }

    public static async Task<List<string>> GetCellClassAsync(IJSRuntime jsRuntime, RgfEntity entityDesc, RgfProperty rgfProperty, RgfDynamicDictionary rowData)
    {
        var list = new List<string>() { rgfProperty.ClientName };
        list.AddRange(await GetCellStyleAsync("RGO_JSColClass", jsRuntime, entityDesc, rgfProperty, rowData));
        return list;
    }
    public static Task<List<string>> GetCellStyleAsync(IJSRuntime jsRuntime, RgfEntity entityDesc, RgfProperty propDesc, RgfDynamicDictionary rowData) => GetCellStyleAsync("RGO_JSColStyle", jsRuntime, entityDesc, propDesc, rowData);
    public static async Task<List<string>> GetCellStyleAsync(string key, IJSRuntime jsRuntime, RgfEntity entityDesc, RgfProperty propDesc, RgfDynamicDictionary rowData)
    {
        var list = new List<string>();
        char separator = key == "RGO_JSColClass" ? ' ' : ';';

        var style = propDesc.Options?.GetStringValue(key == "RGO_JSColClass" ? "RGO_CssClass" : "RGO_Style");
        if (!string.IsNullOrWhiteSpace(style))
        {
            list.AddRange(style.Trim().Split(separator));
        }

        if (propDesc.Options?.TryGetValue(key, out object? fn) == true)
        {
            var jsArgs = CreateJSArgs(entityDesc, rowData, propDesc);
            var css = await jsRuntime.InvokeAsync<string>(RgfBlazorConfiguration.JsBlazorNamespace + ".invokeGridColFuncAsync", fn, jsArgs);
            if (!string.IsNullOrWhiteSpace(css))
            {
                list.AddRange(css.Trim().Split(separator));
            }
        }
        return list;
    }
    #endregion
}
