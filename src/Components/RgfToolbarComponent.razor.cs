﻿using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Text.Json;

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

    public IRgManager Manager { get => EntityParameters.Manager!; }

    public RenderFragment? SettingsMenu { get; set; }

    public RenderFragment? CustomMenu { get; set; }

    public Func<RgfMenu, Task>? MenuRenderCallback { get; set; }

    public RgfToolbarParameters ToolbarParameters { get => EntityParameters.ToolbarParameters; }

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Disposables.Add(Manager.SelectedItems.OnAfterChange(this, (args) => IsSingleSelectedRow = args.NewData?.Count == 1));
        Disposables.Add(Manager.ListHandler.ListDataSource.OnAfterChange(this, (args) => StateHasChanged()));
        MenuRenderCallback = MenuRender;
        CreateSettingsMenu();
        CreateCustomMenu();
    }

    public virtual Task OnToolbarCommand(RgfToolbarEventKind eventKind)
    {
        var eventArgs = new RgfEventArgs<RgfToolbarEventArgs>(this, new RgfToolbarEventArgs(eventKind));
        return ToolbarParameters.EventDispatcher.DispatchEventAsync(eventArgs.Args.EventKind, eventArgs);
    }

    public RenderFragment? CreateSettingsMenu(object? icon = null)
    {
        var menu = new List<RgfMenu>
        {
            new(RgfMenuType.Function, _recroDict.GetRgfUiString("ColSettings"), Menu.ColumnSettings),
            new(RgfMenuType.Function, _recroDict.GetRgfUiString("SaveSettings"), Menu.SaveSettings)
        };
        if (_recroSec.IsAuthenticated && !_recroSec.IsAdmin)
        {
            menu.Add(new(RgfMenuType.Function, _recroDict.GetRgfUiString("ResetSettings"), Menu.ResetSettings));
        }
        menu.Add(new(RgfMenuType.Divider));
        if (RgfBlazorConfiguration.ComponentTypes.TryGetValue(RgfBlazorConfiguration.ComponentType.Chart, out _))
        {
            menu.Add(new(RgfMenuType.Function, "RecroChart", Menu.RecroChart));
        }
        if (Manager.EntityDesc.IsRecroTrackReadable)
        {
            menu.Add(new(RgfMenuType.Function, "RecroTrack", Menu.RecroTrack));
        }
        if (Manager.EntityDesc.Permissions.GetPermission(RgfPermissionType.QueryString))
        {
            menu.Add(new(RgfMenuType.Function, "QueryString", Menu.QueryString));
        }
        if (Manager.EntityDesc.Permissions.GetPermission(RgfPermissionType.QuickWatch))
        {
            menu.Add(new(RgfMenuType.FunctionForRec, "QuickWatch", Menu.QuickWatch));
        }
        if (Manager.EntityDesc.Permissions.GetPermission(RgfPermissionType.Export))
        {
            var export = new RgfMenu()
            {
                MenuType = RgfMenuType.Menu,
                Title = "Export"
            };
            export.NestedMenu.Add(new RgfMenu(RgfMenuType.Function, "Comma-separated values (CSV)", Menu.ExportCsv));
            menu.Add(export);
        }
        /*if (_recroSec.IsAdmin)
        {
            var adminMenu = new List<RgfMenu>();
            menu.Add(new(RgfMenuType.Menu, "Admin") { NestedMenu = adminMenu });
            adminMenu.Add(new(RgfMenuType.Function, "Entity Editor", Menu.EntityEditor));
        }*/
        if ((menu.Count > 0 && menu.Last().MenuType != RgfMenuType.Divider))
        {
            menu.Add(new(RgfMenuType.Divider));
        }
        menu.Add(new(RgfMenuType.Function, "About RecroGrid Framework", Menu.RgfAbout));

        Type? menuType;
        if (!RgfBlazorConfiguration.ComponentTypes.TryGetValue(RgfBlazorConfiguration.ComponentType.Menu, out menuType))
        {
            throw new NotImplementedException("The Menu template component is missing.");
        }
        var param = new RgfMenuParameters()
        {
            MenuItems = menu,
            Navbar = false,
            Icon = icon,
            MenuSelectionCallback = OnSettingsMenu,
            MenuRenderCallback = MenuRenderCallback
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
        Type? type;
        if (!RgfBlazorConfiguration.ComponentTypes.TryGetValue(RgfBlazorConfiguration.ComponentType.Menu, out type))
        {
            throw new NotImplementedException("The Menu template component is missing.");
        }
        var customMenu = Manager.EntityDesc.Options.GetStringValue("RGO_CustomMenu");
        if (!string.IsNullOrEmpty(customMenu))
        {
            var menu = JsonSerializer.Deserialize<RgfMenu>(customMenu, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
            if (menu != null)
            {
                var param = new RgfMenuParameters()
                {
                    MenuItems = menu.NestedMenu,
                    Navbar = false,
                    Icon = icon,
                    MenuSelectionCallback = OnMenuCommand,
                    MenuRenderCallback = MenuRenderCallback
                };
                CustomMenu = builder =>
                {
                    int sequence = 0;
                    builder.OpenComponent(sequence++, type);
                    builder.AddAttribute(sequence++, "MenuParameters", param);
                    builder.CloseComponent();
                };
            }
        }
        return CustomMenu;
    }

    private async Task OnMenuCommand(RgfMenu menu)
    {
        _logger.LogDebug("OnMenuCommand: {type}:{command}", menu.MenuType, menu.Command);
        RgfDynamicDictionary? data = default;
        RgfEntityKey? entityKey = default;
        if (menu.MenuType == RgfMenuType.FunctionForRec && this.IsSingleSelectedRow)
        {
            data = Manager.SelectedItems.Value[0];
            Manager.ListHandler.GetEntityKey(data, out entityKey);
        }
        var eventName = string.IsNullOrEmpty(menu.Command) ? menu.MenuType.ToString() : menu.Command;
        var eventArgs = new RgfEventArgs<RgfMenuEventArgs>(this, new RgfMenuEventArgs(eventName, menu.MenuType, entityKey, data));
        var handled = await ToolbarParameters.MenuEventDispatcher.DispatchEventAsync(eventName, eventArgs);
        if (!handled && !string.IsNullOrEmpty(menu.Command))
        {
            var result = await Manager.ListHandler.CallCustomFunctionAsync(menu.Command, true, null, entityKey);
            if (result == null || !result.Success && result.Messages?.Error?.Any() != true)
            {
                await Manager.NotificationManager.RaiseEventAsync(new RgfUserMessage(_recroDict, UserMessageType.Information, "This menu item is currently not implemented."), this);
            }
            else
            {
                Manager.BroadcastMessages(result.Messages, this);
                if (result.Result.RefreshGrid)
                {
                    await Manager.ListHandler.RefreshDataAsync();
                }
                else if (result.Result.RefreshRow && result.Result.Row != null)
                {
                    await Manager.ListHandler.RefreshRowAsync(result.Result.Row);
                }
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

    public virtual async Task OnSetGridSettingAsync(string? key, string text)
    {
        _logger.LogDebug("OnSetGridSetting: {key}:{text}", key, text);
        if (key != null && int.TryParse(key, out int id))
        {
            var gs = GridSettingList.FirstOrDefault(e => e.GridSettingsId == id);
            if (gs != null && gs.GridSettingsId != 0)
            {
                GridSetting = gs;
                await Manager.ListHandler.RefreshDataAsync(GridSetting.GridSettingsId);
            }
        }
        else
        {
            bool isPublic = GridSetting.IsPublicNonNullable;
            GridSetting = new()
            {
                SettingsName = text,
                IsPublicNonNullable = isPublic
            };
        }
    }

    public virtual async Task<bool> OnSaveGridSettingsAsync()
    {
        var settings = Manager.ListHandler.GetGridSettings();
        settings.GridSettingsId = GridSetting.GridSettingsId;
        settings.SettingsName = GridSetting.SettingsName;
        settings.IsPublic = GridSetting.IsPublic;
        var res = await Manager.SaveGridSettingsAsync(settings);
        if (res != null)
        {
            GridSetting.IsPublic = res.IsPublic;
            if (GridSetting.GridSettingsId == null)
            {
                GridSetting.GridSettingsId = res.GridSettingsId;
                GridSettingList.Add(GridSetting);
            }
            return true;
        }
        return false;
    }

    public virtual async Task<bool> OnDeleteGridSettingsAsync()
    {
        if (GridSetting.GridSettingsId != null && GridSetting.GridSettingsId != 0)
        {
            bool res = await Manager.DeleteGridSettingsAsync((int)GridSetting.GridSettingsId);
            if (res)
            {
                GridSetting = new() { SettingsName = "" };//clear text input
                return true;
            }
        }
        return false;
    }

    private Task MenuRender(RgfMenu menu)
    {
        if (menu.MenuType == RgfMenuType.FunctionForRec)
        {
            menu.Disabled = !IsSingleSelectedRow;
        }
        return Task.CompletedTask;
    }

    public void OnDelete()
    {
        _dynamicDialog.Choice(
            _recroDict.GetRgfUiString("Delete"),
            _recroDict.GetRgfUiString("DelConfirm"),
            [
                new(_recroDict.GetRgfUiString("Yes"), (args) => OnToolbarCommand(RgfToolbarEventKind.Delete)),
                new(_recroDict.GetRgfUiString("No"), isPrimary:true)
            ],
            DialogType.Warning);
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