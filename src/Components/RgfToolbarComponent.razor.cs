using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Mappings;
using System;
using System.Linq;

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

    public Func<RgfMenu, Task>? MenuSelectionCallback { get; set; }

    public Func<RgfMenu, Task>? MenuRenderCallback { get; set; }

    private IRecroDictService RecroDict => Manager.RecroDict;

    public RgfToolbarParameters ToolbarParameters { get => EntityParameters.ToolbarParameters; }

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Disposables.Add(Manager.SelectedItems.OnAfterChange(this, (args) => IsSingleSelectedRow = args.NewData?.Count == 1));
        Disposables.Add(Manager.ListHandler.GridData.OnAfterChange(this, (args) => StateHasChanged()));
        MenuSelectionCallback = SettingsMenuItemSelected;
        MenuRenderCallback = SettingsMenuRender;
        CreateSettingsMenu();
    }

    public virtual void OnToolbarCommand(ToolbarAction command)
    {
        Manager.NotificationManager.RaiseEvent(new RgfToolbarEventArgs(command), this);
    }

    public RenderFragment? CreateSettingsMenu(object? icon = null)
    {
        var menu = new List<RgfMenu>();
        menu.Add(new(RgfMenuType.Function, RecroDict.GetRgfUiString("ColSettings"), Menu.ColumnSettings));
        menu.Add(new(RgfMenuType.Function, RecroDict.GetRgfUiString("SaveSettings"), Menu.SaveSettings));
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

    private Task SettingsMenuItemSelected(RgfMenu menu)
    {
        var action = Toolbar.MenuCommand2ToolbarAction(menu.Command);
        if (action != ToolbarAction.Invalid)
        {
            OnToolbarCommand(action);
        }
        else
        {
            if(menu.Command == Menu.EntityEditor)
            {
                Manager.NotificationManager.RaiseEvent(new RgfMenuEventArgs(menu.Command), this);
            }
        }
        return Task.CompletedTask;
    }

    private Task SettingsMenuRender(RgfMenu menu)
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
            new List<ButtonParameters>()
            {
                    new ButtonParameters(RecroDict.GetRgfUiString("Yes"), (args) => OnToolbarCommand(ToolbarAction.Delete)),
                    new ButtonParameters(RecroDict.GetRgfUiString("No"), isPrimary:true)
            },
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
