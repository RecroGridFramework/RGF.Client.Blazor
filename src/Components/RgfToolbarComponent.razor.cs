﻿using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Mappings;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfToolbarComponent : ComponentBase, IDisposable
{
    public List<IDisposable> Disposables { get; private set; } = new();

    public RgfSelectParam? SelectParam => Manager.SelectParam;

    public bool IsFiltered => Manager.IsFiltered;

    public BasePermissions BasePermissions => Manager.ListHandler.CRUD;

    public bool IsSingleSelectedRow { get; private set; } = false;

    public IRgManager Manager { get => EntityParameters.Manager!; }

    public RenderFragment? SettingsMenu { get; set; }

    public RenderFragment? CustomMenu { get; set; }

    public Func<RgfMenu, Task>? MenuSelectionCallback { get; set; }

    public Func<RgfMenu, Task>? MenuRenderCallback { get; set; }

    private IRecroDictService RecroDict => Manager.RecroDict;

    public RgfToolbarParameters ToolbarParameters { get => EntityParameters.ToolbarParameters; }

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Disposables.Add(Manager.SelectedItems.OnAfterChange(this, (args) => IsSingleSelectedRow = args.NewData?.Count == 1));
        Disposables.Add(Manager.ListHandler.ListDataSource.OnAfterChange(this, (args) => StateHasChanged()));
        MenuSelectionCallback = MenuItemSelected;
        MenuRenderCallback = MenuRender;
        CreateSettingsMenu();
        CreateCustomMenu();
    }

    public virtual void OnToolbarCommand(ToolbarAction command)
    {
        Manager.NotificationManager.RaiseEvent(new RgfToolbarEventArgs(command), this);
    }

    public RenderFragment? CreateSettingsMenu(object? icon = null)
    {
        var menu = new List<RgfMenu>
        {
            new(RgfMenuType.Function, RecroDict.GetRgfUiString("ColSettings"), Menu.ColumnSettings),
            new(RgfMenuType.Function, RecroDict.GetRgfUiString("SaveSettings"), Menu.SaveSettings)
        };
        if (Manager.RecroSec.IsAuthenticated && !Manager.RecroSec.IsAdmin)
        {
            menu.Add(new(RgfMenuType.Function, RecroDict.GetRgfUiString("ResetSettings"), Menu.ResetSettings));
        }
        menu.Add(new(RgfMenuType.Divider));
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
        if (Manager.RecroSec.IsAdmin)
        {
            var adminMenu = new List<RgfMenu>();
            menu.Add(new(RgfMenuType.Menu, "Admin") { NestedMenu = adminMenu });
            adminMenu.Add(new(RgfMenuType.Function, "Entity Editor", Menu.EntityEditor));
        }
        if ((menu.Count > 0 && menu.Last().MenuType != RgfMenuType.Divider))
        {
            menu.Add(new(RgfMenuType.Divider));
        }
        menu.Add(new(RgfMenuType.Function, "About RecroGrid Framework", Menu.RgfAbout));

        Type? type;
        if (!RgfBlazorConfiguration.ComponentTypes.TryGetValue(RgfBlazorConfiguration.ComponentType.Menu, out type))
        {
            throw new NotImplementedException("The Menu template component is missing.");
        }
        var param = new RgfMenuParameters()
        {
            MenuItems = menu,
            Navbar = false,
            Icon = icon,
            MenuSelectionCallback = MenuSelectionCallback,
            MenuRenderCallback = MenuRenderCallback
        };
        SettingsMenu = builder =>
        {
            int sequence = 0;
            builder.OpenComponent(sequence++, type);
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
                    MenuSelectionCallback = MenuSelectionCallback,
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

    private Task MenuItemSelected(RgfMenu menu)
    {
        var action = Toolbar.MenuCommand2ToolbarAction(menu.Command);
        if (action != ToolbarAction.Invalid)
        {
            OnToolbarCommand(action);
        }
        else
        {
            RgfDynamicDictionary? data = default;
            RgfEntityKey? entityKey = default;
            if (menu.MenuType == RgfMenuType.FunctionForRec && this.IsSingleSelectedRow)
            {
                data = Manager.SelectedItems.Value[0];
                Manager.ListHandler.GetEntityKey(data, out entityKey);
            }
            var arg = new RgfMenuEventArgs(menu.Command, menu.MenuType, entityKey, data);
            Manager.NotificationManager.RaiseEvent(arg, this);
        }
        return Task.CompletedTask;
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
            RecroDict.GetRgfUiString("Delete"),
            RecroDict.GetRgfUiString("DelConfirm"),
            [
                new(RecroDict.GetRgfUiString("Yes"), (args) => OnToolbarCommand(ToolbarAction.Delete)),
                new(RecroDict.GetRgfUiString("No"), isPrimary:true)
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