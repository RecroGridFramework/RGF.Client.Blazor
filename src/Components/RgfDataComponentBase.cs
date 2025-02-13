using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Models;
using System.Globalization;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public class RgfDataComponentBase : ComponentBase, IDisposable
{
    [Parameter, EditorRequired]
    public RgfEntityParameters EntityParameters { get; set; } = null!;

    [Parameter, EditorRequired]
    public RenderFragment<RgfDataComponentBase> ColumnSettingsTemplate { get; set; } = null!;

    [Parameter]
    public RenderFragment<RgfGridColumnParameters>? ColumnTemplate { get; set; }

    public RenderFragment<RgfGridColumnParameters> DefaultColumnTemplate => (param) => builder =>
    {
        builder.OpenComponent<RgfGridColumnComponent>(0);
        builder.AddAttribute(1, "GridColumnParameters", param);
        builder.CloseComponent();
    };

    [Inject]
    private ILogger<RgfDataComponentBase> _logger { get; set; } = default!;

    [Inject]
    protected IJSRuntime _jsRuntime { get; set; } = default!;

    [Inject]
    protected IRecroDictService _recroDict { get; set; } = null!;

    [Inject]
    protected IRecroSecService _recroSec { get; set; } = null!;

    public List<IDisposable> Disposables { get; private set; } = [];

    public ObservableProperty<List<RgfDynamicDictionary>> GridDataSource { get; protected set; } = new([], nameof(GridDataSource));

    public List<RgfDynamicDictionary> GridData => GridDataSource.Value;

    public bool IsProcessing => _isProcessing || Manager.ListHandler.IsLoading.Value;

    public Dictionary<int, RgfEntityKey> SelectedItems { get => Manager.SelectedItems.Value; set => Manager.SelectedItems.Value = value; }

    public List<RgfDynamicDictionary> SelectedRowsData => Manager.GetSelectedRowsData();

    public IRgManager Manager => EntityParameters.Manager!;

    protected RgfDynamicDialog _dynamicDialog { get; set; } = default!;

    protected bool _isProcessing;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        Disposables.Add(Manager.ListHandler.ListDataSource.OnBeforeChange(this, (arg) => _isProcessing = true));
        Disposables.Add(Manager.ListHandler.ListDataSource.OnAfterChange(this, (arg) => Task.Run(() => OnChangedGridDataAsync(arg))));
        Disposables.Add(Manager.ListHandler.IsLoading.OnAfterChange(this, (arg) => StateHasChanged()));

        EntityParameters.ToolbarParameters.MenuEventDispatcher.Subscribe([Menu.QueryString, Menu.QuickWatch, Menu.RecroTrack, Menu.ExportCsv], OnMenuCommandAsync, true);
        EntityParameters.ToolbarParameters.EventDispatcher.Subscribe(RgfToolbarEventKind.ToggleQuickFilter, OnToggleQuickFilter);

        await OnChangedGridDataAsync(new(GridData, Manager.ListHandler.ListDataSource.Value));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        _logger.LogDebug($"OnAfterRender first:{firstRender}");

        var eventArg = new RgfEventArgs<RgfListEventArgs>(this, RgfListEventArgs.CreateAfterRenderEvent(this, firstRender));
        await EntityParameters.GridParameters.EventDispatcher.DispatchEventAsync(eventArg.Args.EventKind, eventArg);
    }

    protected async Task OnMenuCommandAsync(IRgfEventArgs<RgfMenuEventArgs> arg)
    {
        switch (arg.Args.Command)
        {
            case Menu.QueryString:
                ShowQueryString();
                arg.Handled = true;
                break;

            case Menu.QuickWatch:
                QuickWatch();
                arg.Handled = true;
                break;

            case Menu.RecroTrack:
                RecroTrack();
                arg.Handled = true;
                break;

            case Menu.ExportCsv:
                await ExportCsvAsync();
                arg.Handled = true;
                break;
        }
    }

    protected void OnToggleQuickFilter(IRgfEventArgs<RgfToolbarEventArgs> args)
    {
        StateHasChanged();
    }

    protected void ShowQueryString()
    {
        RgfDialogParameters parameters = new()
        {
            Title = "QueryString",
            ShowCloseButton = true,
            Resizable = true,
            Width = "800px",
            Height = "600px",
            ContentTemplate = (builder) =>
            {
                int sequence = 0;
                builder.OpenElement(sequence++, "textarea");
                builder.AddAttribute(sequence++, "type", "text");
                builder.AddAttribute(sequence++, "style", "width:100%;height:100%;");
                builder.AddContent(sequence++, Manager.ListHandler.QueryString ?? "?");
                builder.CloseElement();
            }
        };
        _dynamicDialog.Dialog(parameters);
    }

    protected void QuickWatch()
    {
        _logger.LogDebug("RgfGridComponent.QuickWatch");
        var entityKey = SelectedItems.FirstOrDefault().Value;
        if (entityKey?.IsEmpty == false)
        {
            var param = new RgfEntityParameters("QuickWatch", Manager.SessionParams);
            param.FormParameters.FormViewKey.EntityKey = entityKey;
            RgfDialogParameters dialogParameters = new()
            {
                ShowCloseButton = true,
                Resizable = true,
                UniqueName = "quickwatch",
                ContentTemplate = RgfEntityComponent.Create(param, _logger),
            };
            _dynamicDialog.Dialog(dialogParameters);
        }
    }

    protected async Task ExportCsvAsync()
    {
        CultureInfo culture = _recroSec.UserCultureInfo();
        var listSeparator = culture.TextInfo.ListSeparator;
        var customParams = new Dictionary<string, object> { { "ListSeparator", listSeparator } };
        var toast = RgfToastEventArgs.CreateActionEvent(_recroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, "Export", delay: 0);
        await Manager.ToastManager.RaiseEventAsync(toast, this);
        var result = await Manager.ListHandler.CallCustomFunctionAsync(new RgfCustomFunctionContext()
        {
            FunctionName = Menu.ExportCsv,
            RequireQueryParams = true,
            CustomParams = customParams
        });
        if (result != null)
        {
            await Manager.BroadcastMessages(result.Messages, this);
            if (result.Result?.Results != null)
            {
                var stream = await Manager.GetResourceAsync<Stream>("export.csv", new Dictionary<string, string>() {
                    { "sessionId", Manager.SessionParams.SessionId },
                    { "id", result.Result.Results.ToString()! }
                });
                if (stream != null)
                {
                    await Manager.ToastManager.RaiseEventAsync(toast.RecreateAsSuccess(_recroDict.GetRgfUiString("Processed")), this);
                    using var streamRef = new DotNetStreamReference(stream);
                    await _jsRuntime.InvokeVoidAsync(RgfBlazorConfiguration.JsBlazorNamespace + ".downloadFileFromStream", $"{Manager.EntityDesc.MenuTitle}.csv", streamRef);
                    return;
                }
            }
            await Manager.ToastManager.RaiseEventAsync(toast.Remove(), this);
        }
    }

    protected void RecroTrack()
    {
        _logger.LogDebug("RgfGridComponent.RecroTrack");
        var param = new RgfEntityParameters("RecroTrack", Manager.SessionParams);
        var entityKey = SelectedItems.FirstOrDefault().Value;
        if (entityKey?.IsEmpty == false)
        {
            param.FormParameters.FormViewKey.EntityKey = entityKey;
        }
        RgfDialogParameters dialogParameters = new()
        {
            ShowCloseButton = true,
            Resizable = true,
            UniqueName = "recrotrack",
            ContentTemplate = RgfEntityComponent.Create(param, _logger),
        };
        _dynamicDialog.Dialog(dialogParameters);
    }

    public RenderFragment CreateColumnSettings()
    {
        if (EntityParameters.GridParameters.ColumnSettingsTemplate != null)
        {
            return EntityParameters.GridParameters.ColumnSettingsTemplate(this);
        }
        return ColumnSettingsTemplate(this);
    }

    public RenderFragment RenderContentItem(RgfProperty propDesc, RgfDynamicDictionary rowData)
    {
        var param = new RgfGridColumnParameters(this, propDesc, rowData);
        if (EntityParameters.GridParameters.ColumnTemplate != null)
        {
            return EntityParameters.GridParameters.ColumnTemplate(param);
        }
        return ColumnTemplate != null ? ColumnTemplate(param) : DefaultColumnTemplate(param);
    }

    private void CreateAttributes(RgfEntity entityDesc, RgfDynamicDictionary rowData)
    {
        var rgparams = rowData.Get<Dictionary<string, object>>("__rgparams");
        if (rgparams?.TryGetValue("Options", out var op) == true && op is Dictionary<string, object> options)
        {
            _logger.LogDebug("CreateAttributes");
            var attributes = rowData.GetOrNew<RgfDynamicDictionary>("__attributes");
            foreach (var option in options.Where(o => o.Value != null))
            {
                if (option.Value is Dictionary<string, object> propOptions)
                {
                    foreach (var propOption in propOptions.Where(o => o.Value != null))
                    {
                        var prop = entityDesc.Properties.FirstOrDefault(e => e.Alias.Equals(option.Key, StringComparison.OrdinalIgnoreCase) || e.ClientName.Equals(option.Key, StringComparison.OrdinalIgnoreCase));
                        if (prop != null)
                        {
                            var propAttributes = attributes.GetOrNew<RgfDynamicDictionary>(prop.Alias);
                            if (propOption.Key.Equals("class", StringComparison.OrdinalIgnoreCase) || propOption.Key.Equals("RGOD_CssClass", StringComparison.OrdinalIgnoreCase))
                            {
                                propAttributes.Set<string>("class", (old) => string.IsNullOrEmpty(old) ? propOption.Value.ToString()! : old.EnsureContains(propOption.Value.ToString(), ' '));
                            }
                            else if (propOption.Key.Equals("style", StringComparison.OrdinalIgnoreCase) || propOption.Key.Equals("RGOD_Style", StringComparison.OrdinalIgnoreCase))
                            {
                                propAttributes.Set<string>("style", (old) => string.IsNullOrEmpty(old) ? propOption.Value.ToString()! : old.EnsureContains(propOption.Value.ToString(), ';'));
                            }
                        }
                    }
                }
                else
                {
                    if (option.Key.Equals("class", StringComparison.OrdinalIgnoreCase) || option.Key.Equals("RGOD_CssClass", StringComparison.OrdinalIgnoreCase))
                    {
                        attributes.Set<string>("class", (old) => string.IsNullOrEmpty(old) ? option.Value.ToString()! : old.EnsureContains(option.Value.ToString(), ' '));
                    }
                    else if (option.Key.Equals("style", StringComparison.OrdinalIgnoreCase) || option.Key.Equals("RGOD_Style", StringComparison.OrdinalIgnoreCase))
                    {
                        attributes.Set<string>("style", (old) => string.IsNullOrEmpty(old) ? option.Value.ToString()! : old.EnsureContains(option.Value.ToString(), ';'));
                    }
                }
            }
        }
    }

    public RgfDynamicData? GetColumnData(int absoluteRowIndex, string alias)
    {
        var rowData = Manager.ListHandler.GetRowData(absoluteRowIndex);
        return rowData?.GetItemData(alias);
    }

    public RgfDynamicData? GetColumnData(int absoluteRowIndex, int propertyId)
    {
        var alias = Manager.EntityDesc.Properties.FirstOrDefault(e => e.Id == propertyId)?.Alias;
        if (string.IsNullOrEmpty(alias))
        {
            return null;
        }
        return GetColumnData(absoluteRowIndex, alias);
    }

    protected virtual async Task OnChangedGridDataAsync(ObservablePropertyEventArgs<List<RgfDynamicDictionary>> args)
    {
        try
        {
            _logger.LogDebug("OnChangeData");
            var entityDesc = Manager.EntityDesc;
            var rgo = new string[] { "RGO_CssClass", "RGO_Style", "RGO_JSRowClass", "RGO_JSRowStyle" };
            var prop4RowStyles = entityDesc.Properties.Where(e => e.Options?.Any(e => rgo.Contains(e.Key)) == true).ToArray();
            var prop4ColStyles = entityDesc.SortedVisibleColumns.Where(e => e.Options?.Any(e => rgo.Contains(e.Key)) == true).ToArray();
            foreach (var rowData in args.NewData)
            {
                CreateAttributes(entityDesc, rowData);
                await RgfGridColumnComponent.InitStylesAsync(_jsRuntime, entityDesc, rowData, prop4RowStyles, prop4ColStyles);
                var eventArgs = new RgfListEventArgs(RgfListEventKind.CreateRowData, this, rowData);
                await EntityParameters.GridParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfListEventArgs>(this, eventArgs));
            }
            GridDataSource.Value = args.NewData;
            if (EntityParameters.DisplayMode == RfgDisplayMode.Tree ||
                SelectedItems.Any() && EntityParameters.GridParameters.EnableMultiRowSelection != true)
            {
                await Manager.SelectedItems.SetValueAsync(new());
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    public virtual void Dispose()
    {
        EntityParameters.ToolbarParameters.MenuEventDispatcher.Unsubscribe([Menu.QueryString, Menu.QuickWatch, Menu.RecroTrack, Menu.ExportCsv], OnMenuCommandAsync);
        EntityParameters.ToolbarParameters.EventDispatcher.Unsubscribe(RgfToolbarEventKind.ToggleQuickFilter, OnToggleQuickFilter);

        if (Disposables != null)
        {
            Disposables.ForEach(disposable => disposable.Dispose());
            Disposables = null!;
        }
    }
}
