using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.Blazor.Handlers;

public class MenuRenderer
{
    private readonly RgfEntityParameters _entityParameters;

    public MenuRenderer(RgfEntityParameters entityParameters)
    {
        this._entityParameters = entityParameters;
        OnMenuRender = MenuRender;
    }

    private IRgManager Manager => _entityParameters.Manager ?? throw new InvalidOperationException("Manager is not set.");

    public Func<RgfMenu, Task>? OnMenuRender { get; set; }

    public RenderFragment? CreateCustomMenu(Func<RgfMenu, Task>? handleMenuCommand, object? icon = null)
    {
        Type menuType = RgfBlazorConfiguration.GetComponentType(RgfBlazorConfiguration.ComponentType.Menu);
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
                    OnMenuItemSelect = handleMenuCommand,
                    OnMenuRender = OnMenuRender,
                    HideOnMouseLeave = true
                };
                return builder =>
                {
                    int sequence = 0;
                    builder.OpenComponent(sequence++, menuType);
                    builder.AddAttribute(sequence++, "MenuParameters", param);
                    builder.CloseComponent();
                };
            }
        }
        return null;
    }

    private Task MenuRender(RgfMenu menu)
    {
        menu.Disabled = _entityParameters.DisplayMode == RfgDisplayMode.Tree && new string[] { Menu.ColumnSettings, Menu.SaveSettings, Menu.ResetSettings }.Contains(menu.Command);
        if (menu.MenuType == RgfMenuType.FunctionForRec)
        {
            menu.Disabled = Manager.SelectedItems.Value.Count == 0 || (_entityParameters.GridParameters.EnableMultiRowSelection != true && Manager.SelectedItems.Value.Count > 1);
        }
        return Task.CompletedTask;
    }
}