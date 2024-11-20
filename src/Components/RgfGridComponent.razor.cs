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
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfGridComponent : ComponentBase, IDisposable
{
    [Inject]
    private ILogger<RgfGridComponent> _logger { get; set; } = default!;

    [Inject]
    private IJSRuntime _jsRuntime { get; set; } = default!;

    [Inject]
    private IRecroDictService _recroDict { get; set; } = null!;

    [Inject]
    private IRecroSecService _recroSec { get; set; } = null!;

    public List<IDisposable> Disposables { get; private set; } = [];

    public ObservableProperty<List<RgfDynamicDictionary>> GridDataSource { get; private set; } = new([], nameof(GridDataSource));

    public List<RgfDynamicDictionary> GridData => GridDataSource.Value;

    public bool IsProcessing => _isProcessing || Manager.ListHandler.IsLoading.Value;

    public List<RgfDynamicDictionary> SelectedItems { get => Manager.SelectedItems.Value; set => Manager.SelectedItems.Value = value; }

    public IRgManager Manager { get => EntityParameters.Manager!; }

    public RgfGridParameters GridParameters { get => EntityParameters.GridParameters; }

    private RgfDynamicDialog _dynamicDialog { get; set; } = default!;

    private RenderFragment? _headerMenu;

    private bool _isProcessing;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        EntityParameters.ToolbarParameters.MenuEventDispatcher.Subscribe([Menu.QueryString, Menu.QuickWatch, Menu.RecroTrack, Menu.ExportCsv], OnMenuCommandAsync, true);

        Disposables.Add(Manager.ListHandler.ListDataSource.OnBeforeChange(this, (arg) => _isProcessing = true));
        Disposables.Add(Manager.ListHandler.ListDataSource.OnAfterChange(this, (arg) => Task.Run(() => OnChangedGridDataAsync(arg))));
        Disposables.Add(Manager.ListHandler.IsLoading.OnAfterChange(this, (arg) => StateHasChanged()));

        await OnChangedGridDataAsync(new(GridData, Manager.ListHandler.ListDataSource.Value));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        var eventArg = new RgfEventArgs<RgfListEventArgs>(this, RgfListEventArgs.CreateAfterRenderEvent(this, firstRender));
        await GridParameters.EventDispatcher.DispatchEventAsync(eventArg.Args.EventKind, eventArg);
        _logger.LogDebug("OnAfterRender");
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

    public RenderFragment ShowHeaderMenu(int propertyId, Point menuPosition)
    {
        var menu = new List<RgfMenu>();
        var prop = Manager.EntityDesc.Properties.FirstOrDefault(e => e.Id == propertyId);
        bool clientMode = Manager.EntityDesc.Options.GetBoolValue("RGO_ClientMode") == true;
        if (!clientMode && prop?.ListType == PropertyListType.Numeric)
        {
            menu.Add(new(RgfMenuType.Function, _recroDict.GetRgfUiString("Aggregates"), Menu.Aggregates) { Scope = propertyId.ToString() });
        }
        if (menu.Count > 0)
        {
            menu.Add(new(RgfMenuType.Divider));
        }
        menu.Add(new(RgfMenuType.Function, _recroDict.GetRgfUiString("ColSettings"), Menu.ColumnSettings));

        var param = new RgfMenuParameters()
        {
            MenuItems = menu,
            Navbar = false,
            OnMenuItemSelect = OnHeaderMenuCommand,
            ContextMenuPosition = menuPosition,
            OnMouseLeave = () =>
            {
                _headerMenu = null;
                StateHasChanged();
            }
        };
        Type menuType = RgfBlazorConfiguration.GetComponentType(RgfBlazorConfiguration.ComponentType.Menu);
        _headerMenu = builder =>
        {
            int sequence = 0;
            builder.OpenComponent(sequence++, menuType);
            builder.AddAttribute(sequence++, "MenuParameters", param);
            builder.CloseComponent();
        };
        return _headerMenu;
    }

    private async Task OnHeaderMenuCommand(RgfMenu menu)
    {
        _logger.LogDebug("OnHeaderMenuCommand: {type}:{command}", menu.MenuType, menu.Command);
        _headerMenu = null;
        StateHasChanged();

        switch (menu.Command)
        {
            case Menu.ColumnSettings:
                {
                    var eventName = string.IsNullOrEmpty(menu.Command) ? menu.MenuType.ToString() : menu.Command;
                    var eventArgs = new RgfEventArgs<RgfMenuEventArgs>(this, new RgfMenuEventArgs(eventName, menu.Title, menu.MenuType));
                    await EntityParameters.ToolbarParameters.MenuEventDispatcher.DispatchEventAsync(eventName, eventArgs);
                    return;
                }

            case Menu.Aggregates:
                if (int.TryParse(menu.Scope, out var propertyId))
                {
                    await AggregatesAsync(propertyId);
                }
                break;
        }
    }

    private async Task AggregatesAsync(int propertyId)
    {
        var prop = Manager.EntityDesc.Properties.FirstOrDefault(e => e.Id == propertyId);
        if (prop?.ListType != PropertyListType.Numeric)
        {
            return;
        }

        var aggregates = new List<string>() { "Count", "Sum", "Avg", "Min", "Max" };
        aggregates.RemoveAll(item => !RgfAggregationColumn.AllowedAggregates.Contains(item));
        var aggregateParam = new RgfAggregationSettings()
        {
            Columns = aggregates.Select(e => new RgfAggregationColumn() { Aggregate = e, Id = propertyId }).ToList()
        };

        var res = await Manager.GetAggregateDataAsync(Manager.ListHandler.CreateAggregateRequest(aggregateParam));
        if (!res.Success)
        {
            if (res.Messages?.Error != null)
            {
                foreach (var item in res.Messages.Error)
                {
                    if (item.Key.Equals(RgfCoreMessages.MessageDialog))
                    {
                        _dynamicDialog.Alert(_recroDict.GetRgfUiString("Error"), item.Value);
                    }
                }
            }
        }
        else
        {
            CultureInfo culture = _recroSec.UserCultureInfo();
            var details = new StringBuilder("<div class=\"aggregates\" rgf-grid-comp><table class=\"table\" rgf-grid-comp>");
            foreach (var item in aggregates)
            {
                var title = _recroDict.GetRgfUiString(item == "Count" ? "ItemCount" : item);
                int idx = Array.FindIndex(res.Result.DataColumns, col => col.EndsWith("_" + item));
                var data = res.Result.Data[0][idx];
                try
                {
                    var number = new RgfDynamicData(data).TryGetDecimal();
                    if (number != null)
                    {
                        data = ((decimal)number).ToString("#,0.##", culture);
                    }
                }
                catch { }
                details.AppendLine($"<tr><th>{title}</th><td rgf-grid-comp>{data}</td></tr>");
            }
            details.AppendLine("</table></div>");
            var detailsStr = details.ToString();
            RgfDialogParameters parameters = new()
            {
                Title = _recroDict.GetRgfUiString("Aggregates"),
                ShowCloseButton = true,
                ContentTemplate = (builder) =>
                {
                    int sequence = 0;
                    builder.AddMarkupContent(sequence++, detailsStr);
                }
            };
            _dynamicDialog.Dialog(parameters);
        }
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
        var data = SelectedItems.FirstOrDefault();
        if (data != null && Manager.ListHandler.GetEntityKey(data, out var entityKey) && entityKey != null)
        {
            var param = new RgfEntityParameters("QuickWatch", Manager.SessionParams);
            param.FormParameters.FormViewKey.EntityKey = entityKey;
            RgfDialogParameters dialogParameters = new()
            {
                IsModal = false,
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
        var toast = RgfToastEvent.CreateActionEvent(_recroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, "Export", delay: 0);
        await Manager.ToastManager.RaiseEventAsync(toast, this);
        var result = await Manager.ListHandler.CallCustomFunctionAsync(Menu.ExportCsv, true, customParams);
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
                    await Manager.ToastManager.RaiseEventAsync(RgfToastEvent.RecreateToastWithStatus(toast, _recroDict.GetRgfUiString("Processed"), RgfToastType.Success), this);
                    using var streamRef = new DotNetStreamReference(stream);
                    await _jsRuntime.InvokeVoidAsync(RgfBlazorConfiguration.JsBlazorNamespace + ".downloadFileFromStream", $"{Manager.EntityDesc.Title}.csv", streamRef);
                    return;
                }
            }
            await Manager.ToastManager.RaiseEventAsync(RgfToastEvent.RemoveToast(toast), this);
        }
    }

    protected void RecroTrack()
    {
        _logger.LogDebug("RgfGridComponent.RecroTrack");
        var param = new RgfEntityParameters("RecroTrack", Manager.SessionParams);
        var data = SelectedItems.FirstOrDefault();
        if (data != null && Manager.ListHandler.GetEntityKey(data, out var entityKey) && entityKey != null)
        {
            param.FormParameters.FormViewKey.EntityKey = entityKey;
        }
        RgfDialogParameters dialogParameters = new()
        {
            IsModal = false,
            ShowCloseButton = true,
            Resizable = true,
            UniqueName = "recrotrack",
            ContentTemplate = RgfEntityComponent.Create(param, _logger),
        };
        _dynamicDialog.Dialog(dialogParameters);
    }

    public RenderFragment CreateColumnSettings()
    {
        if (GridParameters.ColumnSettingsTemplate != null)
        {
            return GridParameters.ColumnSettingsTemplate(this);
        }
        return ColumnSettingsTemplate(this);
    }

    public RenderFragment CreateGridColumn(RgfProperty propDesc, RgfDynamicDictionary rowData)
    {
        var param = new RgfGridColumnParameters(this, propDesc, rowData);
        if (GridParameters.ColumnTemplate != null)
        {
            return GridParameters.ColumnTemplate(param);
        }
        return ColumnTemplate(param);
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
                                propAttributes.Set<string>("class", (old) => string.IsNullOrEmpty(old) ? propOption.Value.ToString()! : $"{old.Trim()} {propOption.Value}");
                            }
                            else if (propOption.Key.Equals("style", StringComparison.OrdinalIgnoreCase) || propOption.Key.Equals("RGOD_Style", StringComparison.OrdinalIgnoreCase))
                            {
                                propAttributes.Set<string>("style", (old) => string.IsNullOrEmpty(old) ? propOption.Value.ToString()! : $"{old.Trim(';')};{propOption.Value}");
                            }
                        }
                    }
                }
                else
                {
                    if (option.Key.Equals("class", StringComparison.OrdinalIgnoreCase) || option.Key.Equals("RGOD_CssClass", StringComparison.OrdinalIgnoreCase))
                    {
                        attributes.Set<string>("class", (old) => string.IsNullOrEmpty(old) ? option.Value.ToString()! : $"{old.Trim()} {option.Value}");
                    }
                    else if (option.Key.Equals("style", StringComparison.OrdinalIgnoreCase) || option.Key.Equals("RGOD_Style", StringComparison.OrdinalIgnoreCase))
                    {
                        attributes.Set<string>("style", (old) => string.IsNullOrEmpty(old) ? option.Value.ToString()! : $"{old.Trim(';')};{option.Value}");
                    }
                }
            }
        }
    }

    public RgfDynamicData? GetColumnData(int rowIndex, string alias)
    {
        var rowData = Manager.ListHandler.GetRowData(rowIndex);
        return rowData?.GetItemData(alias);
    }

    public RgfDynamicData? GetColumnData(int rowIndex, int propertyId)
    {
        var alias = Manager.EntityDesc.Properties.FirstOrDefault(e => e.Id == propertyId)?.Alias;
        if (string.IsNullOrEmpty(alias))
        {
            return null;
        }
        return GetColumnData(rowIndex, alias);
    }

    protected virtual async Task OnChangedGridDataAsync(ObservablePropertyEventArgs<List<RgfDynamicDictionary>> args)
    {
        try
        {
            _logger.LogDebug("OnChangeGridData");
            var entityDesc = Manager.EntityDesc;
            var rgo = new string[] { "RGO_CssClass", "RGO_Style", "RGO_JSRowClass", "RGO_JSRowStyle" };
            var prop4RowStyles = entityDesc.Properties.Where(e => e.Options?.Any(e => rgo.Contains(e.Key)) == true).ToArray();
            var prop4ColStyles = entityDesc.SortedVisibleColumns.Where(e => e.Options?.Any(e => rgo.Contains(e.Key)) == true).ToArray();
            foreach (var rowData in args.NewData)
            {
                CreateAttributes(entityDesc, rowData);
                await RgfGridColumnComponent.InitStylesAsync(_jsRuntime, entityDesc, rowData, prop4RowStyles, prop4ColStyles);
                var eventArgs = new RgfListEventArgs(RgfListEventKind.CreateRowData, this, rowData);
                await GridParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfListEventArgs>(this, eventArgs));
            }
            GridDataSource.Value = args.NewData;

            if (SelectedItems.Any())
            {
                SelectedItems.Clear();
                await Manager.SelectedItems.SendChangeNotificationAsync(new(new(), SelectedItems));
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    public virtual async Task RowSelectHandlerAsync(RgfDynamicDictionary rowData)
    {
        //TODO: __rgparam SelectedItems.Add(rowData);
        SelectedItems = [rowData];
        await Manager.SelectedItems.SendChangeNotificationAsync(new(new(), SelectedItems));
    }

    public virtual async Task RowDeselectHandlerAsync(RgfDynamicDictionary rowData)
    {
        //TODO: __rgparam SelectedItems.Remove(rowData);
        SelectedItems = [];
        await Manager.SelectedItems.SendChangeNotificationAsync(new(new(), SelectedItems));
    }

    public virtual Task OnRecordDoubleClickAsync(RgfDynamicDictionary rowData)
    {
        SelectedItems = [rowData];
        var eventArgs = new RgfEventArgs<RgfToolbarEventArgs>(this, new RgfToolbarEventArgs(Manager.SelectParam != null ? RgfToolbarEventKind.Select : RgfToolbarEventKind.Read));
        return EntityParameters.ToolbarParameters.EventDispatcher.DispatchEventAsync(eventArgs.Args.EventKind, eventArgs);
    }

    public void Dispose()
    {
        EntityParameters.ToolbarParameters.MenuEventDispatcher.Unsubscribe([Menu.QueryString, Menu.QuickWatch, Menu.RecroTrack, Menu.ExportCsv], OnMenuCommandAsync);

        if (Disposables != null)
        {
            Disposables.ForEach(disposable => disposable.Dispose());
            Disposables = null!;
        }
    }
}