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

    [Inject]
    private IRecroDictService _recroDict { get; set; } = null!;

    public IEnumerable<RgfGridColumnSettings> Columns { get; private set; } = [];

    public RgfDialogParameters DialogParameters { get; set; } = new();

    private IRgManager Manager => BaseDataComponent.Manager;

    private RgfEntity? _entityDesc { get; set; }

    private RenderFragment? _settingsDialog { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        BaseDataComponent.EntityParameters.ToolbarParameters.MenuEventDispatcher.Subscribe(Menu.ColumnSettings, ShowColumnSettingsAsync, true);

        DialogParameters.Title = _recroDict.GetRgfUiString("ColSettings");
        DialogParameters.ShowCloseButton = true;
        DialogParameters.CssClass = "rgf-dialog-column-settings";
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
        if (!ReferenceEquals(_entityDesc, Manager.EntityDesc))
        {
            _entityDesc = Manager.EntityDesc;
            Columns = RgfGridColumnSettings.InitColumnSettings(_entityDesc).ToArray();
            foreach (var col in Columns)
            {
                InitializeExternalSettings(col, null);
            }
        }
        else
        {
            RgfGridColumnSettings.UpdateColumnSettingsFromProperties(Columns, _entityDesc);
        }
        Columns = Columns.OrderBy(e => e.ColPosOrNull ?? int.MaxValue).ThenBy(e => e.PathTitle ?? e.Property.ColTitle).ToArray();

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

    public async Task LoadPropertiesAsync(RgfGridColumnSettings settings, RgfGridColumnSettings? parent)
    {
        if (settings.BaseEntity != null)
        {
            return;
        }

        var param = Manager.CreateGridRequest((request) =>
        {
            request.EntityName = settings.Property.BaseEntityNameVersion;
            request.GridId = null;
        });

        var result = await Manager.GetEntityDescAsync(param);
        if (result != null)
        {
            await Manager.BroadcastMessages(result.Messages, this);
        }

        if (result?.Success == true)
        {
            var path = settings.ExternalSettings?.ExternalPath?.Split('/').ToList() ?? [];
            settings.BaseEntity = result.Result;
            settings.RelatedEntityColumnSettings = RgfGridColumnSettings.InitColumnSettings(settings.BaseEntity, true).ToArray();
            foreach (var col in settings.RelatedEntityColumnSettings)
            {
                InitializeExternalSettings(col, settings);
            }
            RgfGridColumnSettings.UpdateColumnSettingsFromProperties(Columns, _entityDesc);
        }
    }

    private void InitializeExternalSettings(RgfGridColumnSettings settings, RgfGridColumnSettings? parent)
    {
        settings.ExternalSettings = new RgfExternalColumnSettings()
        {
            ExternalId = settings.Property.Id
        };
        if (parent != null)
        {
            settings.PathTitle = $"{parent.PathTitle ?? parent.Property.ColTitle} > {settings.Property.ColTitle}";
            settings.ExternalSettings.ExternalPath = $"{parent.ExternalSettings.ExternalPath}";
            if (!string.IsNullOrEmpty(settings.Property.BaseEntityPropertyName))
            {
                settings.ExternalSettings.ExternalPath += $".{settings.Property.BaseEntityPropertyName}";
            }
        }
        else
        {
            settings.ExternalSettings.ExternalPath = settings.Property.BaseEntityPropertyName;
        }
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
            var eventArgs = new RgfListEventArgs(RgfListEventKind.ColumnSettingsChanged, BaseDataComponent, properties: Manager.ListHandler.SortedVisibleColumns);
            await BaseDataComponent.EntityParameters.GridParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfListEventArgs>(this, eventArgs));
        }
    }

    public void Dispose()
    {
        BaseDataComponent.EntityParameters.ToolbarParameters.MenuEventDispatcher.Unsubscribe(Menu.ColumnSettings, ShowColumnSettingsAsync);
    }
}