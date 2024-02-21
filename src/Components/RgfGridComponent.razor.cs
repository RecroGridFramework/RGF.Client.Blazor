using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Events;
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

    public List<IDisposable> Disposables { get; private set; } = new();

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

        Disposables.Add(Manager.NotificationManager.Subscribe<RgfToolbarEventArgs>(this, OnToolbarCommanAsync));
        Disposables.Add(Manager.NotificationManager.Subscribe<RgfMenuEventArgs>(this, OnMenuCommand));
        Disposables.Add(Manager.ListHandler.ListDataSource.OnBeforeChange(this, (args) => _isProcessing = true));
        Disposables.Add(Manager.ListHandler.ListDataSource.OnAfterChange(this, (arg) => Task.Run(() => OnChangedGridDataAsync(arg))));

        await OnChangedGridDataAsync(new(GridData, Manager.ListHandler.ListDataSource.Value));
    }

    protected virtual async Task OnToolbarCommanAsync(IRgfEventArgs<RgfToolbarEventArgs> args)
    {
        switch (args.Args.Command)
        {
            case ToolbarAction.QueryString:
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
                break;

            case ToolbarAction.RgfAbout:
                {
                    var about = await Manager.AboutAsync();
                    RgfDialogParameters parameters = new()
                    {
                        Title = "About RecroGrid Framework",
                        ShowCloseButton = true,
                        ContentTemplate = (builder) =>
                        {
                            int sequence = 0;
                            builder.AddMarkupContent(sequence++, about);
                        }
                    };
                    _dynamicDialog.Dialog(parameters);
                }
                break;

            case ToolbarAction.QuickWatch:
                QuickWatch();
                break;

            case ToolbarAction.ExportCsv:
                await ExportCsvAsync();
                break;

            case ToolbarAction.RecroTrack:
                RecroTrack();
                break;
        }
    }

    private void OnMenuCommand(IRgfEventArgs<RgfMenuEventArgs> arg)
    {
        _logger.LogDebug("OnMenuCommand: {type}:{command}", arg.Args.MenuType, arg.Args.Command);
    }

    protected void QuickWatch()
    {
        _logger.LogDebug("RgfGridComponent.QuickWatch");
        var data = SelectedItems.FirstOrDefault();
        if (data != null && Manager.ListHandler.GetEntityKey(data, out var entityKey) && entityKey != null)
        {
            var param = new RgfEntityParameters("QuickWatch", Manager.SessionParams);
            param.FormParameters.EntityKey = entityKey;
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
            param.FormParameters.EntityKey = entityKey;
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

    protected virtual async Task OnChangedGridDataAsync(ObservablePropertyEventArgs<List<RgfDynamicDictionary>> args)
    {
        try
        {
            _logger.LogDebug("OnChangeGridData");
            var EntityDesc = Manager.EntityDesc;
            var prop4RowStyles = EntityDesc.Properties.Where(e => e.Options?.Any(e => e.Key == "RGO_JSRowClass" || e.Key == "RGO_JSRowStyle") == true).ToArray();
            var prop4ColStyles = EntityDesc.SortedVisibleColumns.Where(e => e.Options?.Any(e => e.Key == "RGO_JSColClass" || e.Key == "RGO_JSColStyle") == true).ToArray();
            foreach (var rowData in args.NewData)
            {
                await RgfGridColumnComponent.InitStylesAsync(_jsRuntime, EntityDesc, rowData, prop4RowStyles, prop4ColStyles);
                var eventArgs = new RgfGridEventArgs(RgfGridEventKind.CreateAttributes, this, rowData: rowData);
                await GridParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfGridEventArgs>(this, eventArgs));
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
        SelectedItems = new List<RgfDynamicDictionary>() { rowData };
        await Manager.SelectedItems.SendChangeNotificationAsync(new(new(), SelectedItems));
    }

    public virtual async Task RowDeselectHandlerAsync(RgfDynamicDictionary rowData)
    {
        //TODO: __rgparam SelectedItems.Remove(rowData);
        SelectedItems = new List<RgfDynamicDictionary>();
        await Manager.SelectedItems.SendChangeNotificationAsync(new(new(), SelectedItems));
    }

    public virtual Task OnRecordDoubleClickAsync(RgfDynamicDictionary rowData)
    {
        SelectedItems = new List<RgfDynamicDictionary>() { rowData };
        Manager.NotificationManager.RaiseEvent(new RgfToolbarEventArgs(Manager.SelectParam != null ? ToolbarAction.Select : ToolbarAction.Read), this);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Disposables != null)
        {
            Disposables.ForEach(disposable => disposable.Dispose());
            Disposables = null!;
        }
    }
}