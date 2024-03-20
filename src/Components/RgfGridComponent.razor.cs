using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfGridComponent : ComponentBase, IDisposable
{
    [Inject]
    private ILogger<RgfGridComponent> _logger { get; set; } = default!;

    [Inject]
    private IJSRuntime _jsRuntime { get; set; } = default!;

    public List<IDisposable> Disposables { get; private set; } = [];

    public ObservableProperty<List<RgfDynamicDictionary>> GridDataSource { get; private set; } = new([], nameof(GridDataSource));

    public List<RgfDynamicDictionary> GridData => GridDataSource.Value;

    public bool IsProcessing => _isProcessing || Manager.ListHandler.IsLoading;

    public List<RgfDynamicDictionary> SelectedItems { get => Manager.SelectedItems.Value; set => Manager.SelectedItems.Value = value; }

    public IRgManager Manager { get => EntityParameters.Manager!; }

    public RgfGridParameters GridParameters { get => EntityParameters.GridParameters; }

    private RgfDynamicDialog _dynamicDialog { get; set; } = default!;

    private bool _isProcessing;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        EntityParameters.ToolbarParameters.MenuEventDispatcher.Subscribe([Menu.QueryString, Menu.QuickWatch, Menu.RecroTrack, Menu.ExportCsv], OnMenuCommandAsync, true);

        Disposables.Add(Manager.ListHandler.ListDataSource.OnBeforeChange(this, (args) => _isProcessing = true));
        Disposables.Add(Manager.ListHandler.ListDataSource.OnAfterChange(this, (arg) => Task.Run(() => OnChangedGridDataAsync(arg))));

        await OnChangedGridDataAsync(new(GridData, Manager.ListHandler.ListDataSource.Value));
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
        var listSeparator = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator;
        var customParams = new Dictionary<string, object> { { "ListSeparator", listSeparator } };
        var result = await Manager.ListHandler.CallCustomFunctionAsync(Menu.ExportCsv, true, customParams);
        if (result != null)
        {
            Manager.BroadcastMessages(result.Messages, this);
            if (result.Result?.Results != null)
            {
                var stream = await Manager.GetResourceAsync<Stream>("export.csv", new Dictionary<string, string>() {
                    { "sessionId", Manager.SessionParams.SessionId },
                    { "id", result.Result.Results.ToString()! }
                });
                if (stream != null)
                {
                    using var streamRef = new DotNetStreamReference(stream);
                    await _jsRuntime.InvokeVoidAsync(RgfBlazorConfiguration.JsBlazorNamespace + ".downloadFileFromStream", $"{Manager.EntityDesc.Title}.csv", streamRef);
                }
            }
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