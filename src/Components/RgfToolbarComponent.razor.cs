using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Handlers;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Models;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfToolbarComponent : ComponentBase, IDisposable
{
    [Inject]
    private ILogger<RgfToolbarComponent> _logger { get; set; } = null!;

    [Inject]
    private IRecroSecService _recroSec { get; set; } = null!;

    [Inject]
    private IRecroDictService _recroDict { get; set; } = null!;

    public List<IDisposable> Disposables { get; private set; } = new();

    public RgfSelectParam? SelectParam => Manager.SelectParam;

    public bool IsFiltered => Manager.IsFiltered;

    public RgfGridSetting GridSetting { get; private set; } = new();

    public List<RgfGridSetting> GridSettingList => Manager.GridSettingList;

    public BasePermissions BasePermissions => Manager.ListHandler.CRUD;

    public bool IsPublicGridSettingAllowed => Manager.EntityDesc.Permissions.GetPermission(RgfPermissionType.PublicGridSetting);

    public bool IsSingleSelectedRow { get; private set; } = false;

    public IRgManager Manager => EntityParameters.Manager!;

    public RenderFragment? SettingsMenu { get; set; }

    public bool EnableChart => Manager.EntityDesc.Options.GetBoolValue("RGO_ClientMode") != true && RgfBlazorConfiguration.TryGetComponentType(RgfBlazorConfiguration.ComponentType.Chart, out _);

    public RenderFragment? CustomMenu { get; protected set; }

    public RgfToolbarParameters ToolbarParameters => EntityParameters.ToolbarParameters;

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    private MenuRenderer? _menuRenderer;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Disposables.Add(Manager.SelectedItems.OnAfterChange(this, (args) => IsSingleSelectedRow = args.NewData?.Count == 1));
        Disposables.Add(Manager.ListHandler.ListDataSource.OnAfterChange(this, (args) => StateHasChanged()));

        CreateCustomMenu();
        CreateSettingsMenu();
    }

    public virtual Task OnToolbarCommand(RgfToolbarEventKind eventKind, RgfDynamicDictionary? data = null)
    {
        var eventArgs = new RgfEventArgs<RgfToolbarEventArgs>(this, new RgfToolbarEventArgs(eventKind, data));
        return ToolbarParameters.EventDispatcher.DispatchEventAsync(eventArgs.Args.EventKind, eventArgs);
    }

    public RenderFragment? CreateSettingsMenu(object? icon = null)
    {
        var menu = new List<RgfMenu>();
        bool clientMode = Manager.EntityDesc.Options.GetBoolValue("RGO_ClientMode") == true;
        if (!clientMode)
        {
            menu.Add(new(RgfMenuType.Function, _recroDict.GetRgfUiString("ColSettings"), Menu.ColumnSettings));
            menu.Add(new(RgfMenuType.Function, _recroDict.GetRgfUiString("SaveSettings"), Menu.SaveSettings));
            if (_recroSec.IsAuthenticated && !_recroSec.IsAdmin)
            {
                menu.Add(new(RgfMenuType.Function, _recroDict.GetRgfUiString("ResetSettings"), Menu.ResetSettings));
            }
            menu.Add(new(RgfMenuType.Divider));
            //if (RgfBlazorConfiguration.TryGetComponentType(RgfBlazorConfiguration.ComponentType.Chart, out _)) { menu.Add(new(RgfMenuType.Function, "RecroChart", Menu.RecroChart)); }
            if (Manager.EntityDesc.IsRecroTrackReadable)
            {
                menu.Add(new(RgfMenuType.Function, "RecroTrack", Menu.RecroTrack));
            }
        }

        if (Manager.ListHandler?.QueryString != null && Manager.EntityDesc.Permissions.GetPermission(RgfPermissionType.QueryString))
        {
            menu.Add(new(RgfMenuType.Function, "QueryString", Menu.QueryString));
        }

        if (!clientMode && Manager.EntityDesc.Permissions.GetPermission(RgfPermissionType.QuickWatch))
        {
            menu.Add(new(RgfMenuType.FunctionForRec, "QuickWatch", Menu.QuickWatch));
        }

        if ((!clientMode || Manager.EntityDesc.EntityName.Equals("RGRecroChart")) && Manager.EntityDesc.Permissions.GetPermission(RgfPermissionType.Export))
        {
            var export = new RgfMenu()
            {
                MenuType = RgfMenuType.Menu,
                Title = "Export"
            };
            export.NestedMenu.Add(new RgfMenu(RgfMenuType.Function, "Comma-separated values (CSV)", Menu.ExportCsv));
            menu.Add(export);
        }

        if ((menu.Count > 0 && menu.Last().MenuType != RgfMenuType.Divider))
        {
            menu.Add(new(RgfMenuType.Divider));
        }

        menu.Add(new(RgfMenuType.Function, "About RecroGrid Framework", Menu.RgfAbout));
        Type menuType = RgfBlazorConfiguration.GetComponentType(RgfBlazorConfiguration.ComponentType.Menu);
        var param = new RgfMenuParameters()
        {
            MenuItems = menu,
            Navbar = false,
            Icon = icon,
            OnMenuItemSelect = OnSettingsMenu,
            OnMenuRender = _menuRenderer!.OnMenuRender,
            HideOnMouseLeave = true
        };
        SettingsMenu = builder =>
        {
            int sequence = 0;
            builder.OpenComponent(sequence++, menuType);
            builder.AddAttribute(sequence++, "MenuParameters", param);
            builder.CloseComponent();
        };
        return SettingsMenu;
    }

    public RenderFragment? CreateCustomMenu(object? icon = null) 
    {
        _menuRenderer ??= new MenuRenderer(EntityParameters);
        CustomMenu = _menuRenderer.CreateCustomMenu(OnMenuCommand, icon);
        return CustomMenu;
    }

    private async Task OnMenuCommand(RgfMenu menu)
    {
        _logger.LogDebug("OnMenuCommand | {type}:{command}", menu.MenuType, menu.Command);
        RgfDynamicDictionary? data = null;
        RgfEntityKey? entityKey = null;
        if (Manager.SelectedItems.Value.Count > 0 && (IsSingleSelectedRow || EntityParameters.GridParameters.EnableMultiRowSelection == true))
        {
            var item = Manager.SelectedItems.Value.First();
            entityKey = item.Value;
            data = Manager.ListHandler.GetRowData(item.Key);
        }
        var eventName = string.IsNullOrEmpty(menu.Command) ? menu.MenuType.ToString() : menu.Command;
        var eventArgs = new RgfEventArgs<RgfMenuEventArgs>(this, new RgfMenuEventArgs(eventName, menu.Title, menu.MenuType, entityKey, data));
        var handled = await ToolbarParameters.MenuEventDispatcher.DispatchEventAsync(eventName, eventArgs);
        if (!handled && !string.IsNullOrEmpty(menu.Command))
        {
            var toast = RgfToastEventArgs.CreateActionEvent(_recroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, menu.Title, delay: 0);
            await Manager.ToastManager.RaiseEventAsync(toast, this);
            var result = await Manager.ListHandler.CallCustomFunctionAsync(new RgfCustomFunctionContext()
            {
                FunctionName = menu.Command,
                RequireQueryParams = true,
                EntityKey = entityKey
            });
            if (result == null)
            {
                await Manager.ToastManager.RaiseEventAsync(toast.Remove(), this);
                await Manager.NotificationManager.RaiseEventAsync(new RgfUserMessageEventArgs(_recroDict, UserMessageType.Information, _recroDict.GetRgfUiString("MenuNotImplemented")), this);
            }
            else if (result.Success == false)
            {
                await Manager.ToastManager.RaiseEventAsync(toast.Recreate(_recroDict.GetRgfUiString("Error"), RgfToastType.Error), this);
            }
            else
            {
                await Manager.ToastManager.RaiseEventAsync(toast.RecreateAsSuccess(_recroDict.GetRgfUiString("Processed")), this);
                if (result.Result.RefreshGrid)
                {
                    await Manager.ListHandler.RefreshDataAsync();
                }
                else if (result.Result.RefreshRow && result.Result.Row != null)
                {
                    await Manager.ListHandler.RefreshRowAsync(result.Result.Row);
                }
            }
            if (result?.Messages != null)
            {
                await Manager.BroadcastMessages(result.Messages, this);
            }
        }
    }

    private async Task OnSettingsMenu(RgfMenu menu)
    {
        switch (menu.Command)
        {
            case Menu.SaveSettings:
                await Manager.SaveGridSettingsAsync(Manager.ListHandler.GetGridSettings());
                break;

            case Menu.ResetSettings:
                await Manager.SaveGridSettingsAsync(new RgfGridSettings(), true);
                break;

            case Menu.RgfAbout:
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

            default:
                await OnMenuCommand(menu);
                break;
        }
    }

    public virtual async Task<bool> OnSetGridSettingAsync(int? gridSettingsId, string name)
    {
        _logger.LogDebug("OnSetGridSetting | {id}:{name}", gridSettingsId, name);
        if (gridSettingsId > 0)
        {
            var gs = GridSettingList.FirstOrDefault(e => e.GridSettingsId == gridSettingsId);
            if (gs?.GridSettingsId > 0)
            {
                GridSetting = gs;
                await Manager.ToastManager.RaiseEventAsync(new RgfToastEventArgs(Manager.EntityDesc.MenuTitle, RgfToastEventArgs.ActionTemplate(_recroDict.GetRgfUiString("ColSettings"), GridSetting.SettingsName), delay: 2000), this);
                await Manager.ListHandler.RefreshDataAsync(GridSetting.GridSettingsId);
                return true;
            }
        }

        GridSetting = new() { SettingsName = name };
        return false;
    }

    public virtual async Task<bool> OnSaveGridSettingsAsync()
    {
        var settings = Manager.ListHandler.GetGridSettings();
        settings.GridSettingsId = GridSetting.GridSettingsId;
        settings.SettingsName = GridSetting.SettingsName;
        settings.RoleId = GridSetting.RoleId;
        var res = await Manager.SaveGridSettingsAsync(settings);
        if (res != null)
        {
            GridSetting.RoleId = res.RoleId;
            if (GridSetting.GridSettingsId == null)
            {
                GridSetting.GridSettingsId = res.GridSettingsId;
                GridSettingList.Insert(0, GridSetting);
            }
            return true;
        }
        return false;
    }

    public void OnDeleteGridSettingsAsync() => _dynamicDialog.PromptDeletionConfirmation(DeleteGridSettingsAsync, $"{_recroDict.GetRgfUiString("Setup")}: {GridSetting.SettingsName}");

    public virtual async Task<bool> DeleteGridSettingsAsync()
    {
        if (GridSetting.GridSettingsId != null && GridSetting.GridSettingsId != 0)
        {
            var toast = RgfToastEventArgs.CreateActionEvent(_recroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, _recroDict.GetRgfUiString("Delete"), GridSetting.SettingsName);
            await Manager.ToastManager.RaiseEventAsync(toast, this);
            bool res = await Manager.DeleteGridSettingsAsync((int)GridSetting.GridSettingsId);
            if (res)
            {
                GridSetting.SettingsName = "";//clear text input
                await Manager.ToastManager.RaiseEventAsync(toast.Recreate(_recroDict.GetRgfUiString("Processed"), RgfToastType.Info), this);
                StateHasChanged();
                return true;
            }
        }
        return false;
    }

    public void OnDelete()
    {
        if (Manager.SelectedItems.Value.Count == 1)
        {
            var selected = Manager.SelectedItems.Value.First();
            var rowData = Manager.ListHandler.GetRowData(selected.Key);
            _dynamicDialog.PromptDeletionConfirmation(() => OnToolbarCommand(RgfToolbarEventKind.Delete, rowData));
        }
        else
        {
            var title = _recroDict.GetRgfUiString("Delete");
            var confirmationMessage = string.Format(_recroDict.GetRgfUiString("DelConfirmBulk"), Manager.SelectedItems.Value.Count);
            _dynamicDialog.PromptActionConfirmation(title, confirmationMessage, ApprovalType.All | ApprovalType.Cancel, async (approval) =>
            {
                if (approval == ApprovalType.All)
                {
                    await Manager.DeleteSelectedItemsAsync();
                }
            });
        }
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