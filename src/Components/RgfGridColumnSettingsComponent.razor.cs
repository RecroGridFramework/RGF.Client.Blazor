using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfGridColumnSettingsComponent : ComponentBase, IDisposable
{
    [Inject]
    private ILogger<RgfGridColumnSettingsComponent> _logger { get; set; } = null!;

    public GridColumnSettings[] Columns { get; private set; } = null!;

    public RgfDialogParameters DialogParameters { get; set; } = new();

    private IRgManager Manager => BaseGridComponent.Manager;

    private IRecroDictService RecroDict => Manager.RecroDict;

    private RenderFragment? _settingsDialog { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        BaseGridComponent.EntityParameters.ToolbarParameters.MenuEventDispatcher.Subscribe(Menu.ColumnSettings, ShowColumnSettingsAsync, true);

        DialogParameters.Title = RecroDict.GetRgfUiString("ColSettings");
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
                new(RecroDict.GetRgfUiString("Cancel"), OnClose),
                new("OK", (arg) => SaveAsync(), true)
            };
        }
    }

    public Task ShowColumnSettingsAsync(IRgfEventArgs<RgfMenuEventArgs> args)
    {
        Columns = Manager.EntityDesc.Properties
            .Where(e => e.Readable && e.ListType != PropertyListType.RecroGrid && e.FormType != PropertyFormType.Entity)
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

        DialogParameters.OnClose = Close; //We'll reset it in case the dialog might have overwritten it
        if (BaseGridComponent.EntityParameters.DialogTemplate != null)
        {
            _settingsDialog = BaseGridComponent.EntityParameters.DialogTemplate(DialogParameters);
        }
        else
        {
            _settingsDialog = RgfDynamicDialog.Create(DialogParameters, _logger);
        }
        args.Handled = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    public void OnClose(MouseEventArgs? arg)
    {
        if (DialogParameters.OnClose != null)
        {
            DialogParameters.OnClose();
        }
        else
        {
            Close();
        }
    }

    private bool Close()
    {
        _logger.LogDebug("RgfGridColumnSettings.Close");
        _settingsDialog = null;
        StateHasChanged();
        return true;
    }

    public async Task SaveAsync()
    {
        OnClose(null);
        bool changed = await Manager.ListHandler.SetVisibleColumnsAsync(Columns);
        if (changed)
        {
            var eventArgs = new RgfListEventArgs(RgfListEventKind.ColumnSettingsChanged, BaseGridComponent, properties: Manager.EntityDesc.SortedVisibleColumns);
            await BaseGridComponent.EntityParameters.GridParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfListEventArgs>(this, eventArgs));
        }
    }

    public void Dispose()
    {
        BaseGridComponent.EntityParameters.ToolbarParameters.MenuEventDispatcher.Unsubscribe(Menu.ColumnSettings, ShowColumnSettingsAsync);
    }
}