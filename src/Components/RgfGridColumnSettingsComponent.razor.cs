using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfGridColumnSettingsComponent : ComponentBase, IDisposable
{
    [Inject]
    private ILogger<RgfGridColumnSettingsComponent> _logger { get; set; } = null!;

    [Inject]
    private IRecroDictService _recroDict { get; set; } = null!;

    public GridColumnSettings[] Columns { get; private set; } = null!;

    public RgfDialogParameters DialogParameters { get; set; } = new();

    private IRgManager Manager => BaseDataComponent.Manager;

    private RenderFragment? _settingsDialog { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        BaseDataComponent.EntityParameters.ToolbarParameters.MenuEventDispatcher.Subscribe(Menu.ColumnSettings, ShowColumnSettingsAsync, true);

        DialogParameters.Title = _recroDict.GetRgfUiString("ColSettings");
        DialogParameters.ShowCloseButton = true;
        DialogParameters.ContentTemplate = SettingsTemplate(this);
        if (FooterTemplate != null)
        {
            DialogParameters.FooterTemplate = FooterTemplate(this);
        }
        else
        {
            DialogParameters.PredefinedButtons = new List<ButtonParameters>()
            {
                new(_recroDict.GetRgfUiString("Cancel"), OnClose),
                new("OK", (arg) => SaveAsync(), true)
            };
        }
    }

    public Task ShowColumnSettingsAsync(IRgfEventArgs<RgfMenuEventArgs> args)
    {
        Columns = Manager.EntityDesc.Properties
            .Where(e => e.Readable && e.ListType != PropertyListType.RecroGrid && e.FormType != PropertyFormType.Entity && e.Options?.GetBoolValue("RGO_AggregationRequired") != true)
            .Select(e => new GridColumnSettings(e))
            .OrderBy(e => e.ColPos == null)
            .ThenBy(e => e.ColPos)
            .ThenBy(e => e.Property.ColTitle)
            .ToArray();

        int i = 0;
        while (i < Columns.Length && Columns[i].ColPos > 0)
        {
            Columns[i].ColPos = ++i;
        }

        DialogParameters.EventDispatcher.Unsubscribe(this);//We'll reset it in case the dialog might have overwritten it
        DialogParameters.EventDispatcher.Subscribe(RgfDialogEventKind.Close, OnDialogCloseAsync, true);
        if (BaseDataComponent.EntityParameters.DialogTemplate != null)
        {
            _settingsDialog = BaseDataComponent.EntityParameters.DialogTemplate(DialogParameters);
        }
        else
        {
            _settingsDialog = RgfDynamicDialog.Create(DialogParameters, _logger);
        }
        args.Handled = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task OnDialogCloseAsync(IRgfEventArgs<RgfDialogEventArgs> args) => CloseDialogAsync();

    private async Task CloseDialogAsync()
    {
        _logger.LogDebug("CloseDialog | EntityName:{EntityName}", BaseDataComponent.EntityParameters.EntityName);
        _settingsDialog = null;
        await DialogParameters.EventDispatcher.RaiseEventAsync(RgfDialogEventKind.Destroy, this);
        StateHasChanged();
    }

    public Task OnClose(MouseEventArgs? args) => DialogParameters.EventDispatcher.RaiseEventAsync(RgfDialogEventKind.Close, this);

    public async Task SaveAsync()
    {
        _logger.LogDebug("Save | EntityName:{EntityName}", BaseDataComponent.EntityParameters.EntityName);
        await CloseDialogAsync();
        bool changed = await Manager.ListHandler.SetVisibleColumnsAsync(Columns);
        if (changed)
        {
            var eventArgs = new RgfListEventArgs(RgfListEventKind.ColumnSettingsChanged, BaseDataComponent, properties: Manager.EntityDesc.SortedVisibleColumns);
            await BaseDataComponent.EntityParameters.GridParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfListEventArgs>(this, eventArgs));
        }
    }

    public void Dispose()
    {
        BaseDataComponent.EntityParameters.ToolbarParameters.MenuEventDispatcher.Unsubscribe(Menu.ColumnSettings, ShowColumnSettingsAsync);
    }
}