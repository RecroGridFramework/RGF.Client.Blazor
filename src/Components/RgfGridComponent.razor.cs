using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
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

    public List<IDisposable> Disposables { get; private set; } = new();

    public List<RgfDynamicDictionary> GridData { get; private set; } = new();

    public List<RgfDynamicDictionary> SelectedItems { get => Manager.SelectedItems.Value; set => Manager.SelectedItems.Value = value; }

    public IRgManager Manager { get => EntityParameters.Manager!; }

    public RgfGridParameters GridParameters { get => EntityParameters.GridParameters; }

    private RgfDynamicDialog _dynamicDialog { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        Disposables.Add(Manager.ListHandler.GridData.OnAfterChange(this, OnChangedGridData));
        Disposables.Add(Manager.NotificationManager.Subscribe<RgfToolbarEventArgs>(this, OnToolbarCommanAsync));

        await OnChangedGridData(new(GridData, Manager.ListHandler.GridData.Value));
    }

    private async Task OnToolbarCommanAsync(IRgfEventArgs<RgfToolbarEventArgs> args)
    {
        switch (args.Args.Command)
        {
            case ToolbarAction.QueryString:
                {
                    RgfDialogParameters parameters = new()
                    {
                        Title = "QueryString",
                        ShowCloseButton = true,
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

    protected virtual async Task OnChangedGridData(ObservablePropertyEventArgs<List<RgfDynamicDictionary>> args)
    {
        _logger.LogDebug("OnChangeGridData");
        var EntityDesc = Manager.EntityDesc;
        foreach (var rowData in args.NewData)
        {
            var list = await RgfGridColumnComponent.GetRowClassAsync(_jsRuntime, EntityDesc, rowData);
            var attributes = new RgfDynamicDictionary();
            attributes["class"] = list.Any() ? string.Join(" ", list) : null;
            list = await RgfGridColumnComponent.GetRowStyleAsync(_jsRuntime, EntityDesc, rowData);
            attributes["style"] = list.Any() ? string.Join(";", list) : null;

            foreach (var prop in EntityDesc.SortedVisibleColumns)
            {
                list = await RgfGridColumnComponent.GetCellClassAsync(_jsRuntime, EntityDesc, prop, rowData);
                attributes[$"class-{prop.Alias}"] = list.Any() ? string.Join(" ", list) : null;
                list = await RgfGridColumnComponent.GetCellStyleAsync(_jsRuntime, EntityDesc, prop, rowData);
                attributes[$"style-{prop.Alias}"] = list.Any() ? string.Join(";", list) : null;
            }
            rowData["__attributes"] = attributes;
            await GridParameters.Events.CreateAttributes.InvokeAsync(new DataEventArgs<RgfDynamicDictionary>(rowData));
        }
        GridData = args.NewData;

        if (SelectedItems.Any())
        {
            SelectedItems.Clear();
            await Manager.SelectedItems.SendChangeNotificationAsync(new(new(), SelectedItems));
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
