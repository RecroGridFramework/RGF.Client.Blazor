using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfGridComponent : RgfDataComponentBase
{
    [Inject]
    private ILogger<RgfGridComponent> _logger { get; set; } = default!;

    public RgfGridParameters GridParameters => EntityParameters.GridParameters;

    private RenderFragment? _headerMenu;

    public RenderFragment ShowHeaderMenu(int propertyId, Point menuPosition)
    {
        var menu = new List<RgfMenu>();
        var prop = Manager.EntityDesc.Properties.FirstOrDefault(e => e.Id == propertyId);
        bool clientMode = Manager.EntityDesc.Options.GetBoolValue("RGO_ClientMode") == true;
        if (!clientMode && prop?.ListType == PropertyListType.Numeric)
        {
            menu.Add(new(RgfMenuType.Function, _recroDict.GetRgfUiString("Aggregates"), Menu.Aggregates) { Scope = propertyId.ToString() });
        }
        if (menu.Count > 0)
        {
            menu.Add(new(RgfMenuType.Divider));
        }
        menu.Add(new(RgfMenuType.Function, _recroDict.GetRgfUiString("ColSettings"), Menu.ColumnSettings));

        var param = new RgfMenuParameters()
        {
            MenuItems = menu,
            Navbar = false,
            OnMenuItemSelect = OnHeaderMenuCommand,
            ContextMenuPosition = menuPosition,
            OnMouseLeave = () =>
            {
                _headerMenu = null;
                StateHasChanged();
                return true;
            }
        };
        Type menuType = RgfBlazorConfiguration.GetComponentType(RgfBlazorConfiguration.ComponentType.Menu);
        _headerMenu = builder =>
        {
            int sequence = 0;
            builder.OpenComponent(sequence++, menuType);
            builder.AddAttribute(sequence++, "MenuParameters", param);
            builder.CloseComponent();
        };
        return _headerMenu;
    }

    private async Task OnHeaderMenuCommand(RgfMenu menu)
    {
        _logger.LogDebug("OnHeaderMenuCommand: {type}:{command}", menu.MenuType, menu.Command);
        _headerMenu = null;
        StateHasChanged();

        switch (menu.Command)
        {
            case Menu.ColumnSettings:
                {
                    var eventName = string.IsNullOrEmpty(menu.Command) ? menu.MenuType.ToString() : menu.Command;
                    var eventArgs = new RgfEventArgs<RgfMenuEventArgs>(this, new RgfMenuEventArgs(eventName, menu.Title, menu.MenuType));
                    await EntityParameters.ToolbarParameters.MenuEventDispatcher.DispatchEventAsync(eventName, eventArgs);
                    return;
                }

            case Menu.Aggregates:
                if (int.TryParse(menu.Scope, out var propertyId))
                {
                    await AggregatesAsync(propertyId);
                }
                break;
        }
    }

    private async Task AggregatesAsync(int propertyId)
    {
        var prop = Manager.EntityDesc.Properties.FirstOrDefault(e => e.Id == propertyId);
        if (prop?.ListType != PropertyListType.Numeric)
        {
            return;
        }

        var aggregates = new List<string>() { "Count", "Sum", "Avg", "Min", "Max" };
        aggregates.RemoveAll(item => !RgfAggregationColumn.AllowedAggregates.Contains(item));
        var aggregateParam = new RgfAggregationSettings()
        {
            Columns = aggregates.Select(e => new RgfAggregationColumn() { Aggregate = e, Id = propertyId }).ToList()
        };

        var res = await Manager.GetAggregateDataAsync(Manager.ListHandler.CreateAggregateRequest(aggregateParam));
        if (!res.Success)
        {
            if (res.Messages?.Error != null)
            {
                foreach (var item in res.Messages.Error)
                {
                    if (item.Key.Equals(RgfCoreMessages.MessageDialog))
                    {
                        _dynamicDialog.Alert(_recroDict.GetRgfUiString("Error"), item.Value);
                    }
                }
            }
        }
        else
        {
            CultureInfo culture = _recroSec.UserCultureInfo();
            var details = new StringBuilder("<div class=\"aggregates\" rgf-grid-comp><table class=\"table\" rgf-grid-comp>");
            foreach (var item in aggregates)
            {
                var title = _recroDict.GetRgfUiString(item == "Count" ? "ItemCount" : item);
                int idx = Array.FindIndex(res.Result.DataColumns, col => col.EndsWith("_" + item));
                var data = res.Result.Data[0][idx];
                try
                {
                    var number = new RgfDynamicData(data).TryGetDecimal();
                    if (number != null)
                    {
                        data = ((decimal)number).ToString("#,0.##", culture);
                    }
                }
                catch { }
                details.AppendLine($"<tr><th>{title}</th><td rgf-grid-comp>{data}</td></tr>");
            }
            details.AppendLine("</table></div>");
            var detailsStr = details.ToString();
            RgfDialogParameters parameters = new()
            {
                Title = _recroDict.GetRgfUiString("Aggregates"),
                ShowCloseButton = true,
                ContentTemplate = (builder) =>
                {
                    int sequence = 0;
                    builder.AddMarkupContent(sequence++, detailsStr);
                }
            };
            _dynamicDialog.Dialog(parameters);
        }
    }

    public virtual async Task RowSelectHandlerAsync(RgfDynamicDictionary rowData)
    {
        var rowIndexAndKey = Manager.ListHandler.GetRowIndexAndKey(rowData);
        if (GridParameters.EnableMultiRowSelection == true)
        {
            var orig = SelectedItems.ToDictionary();
            if (SelectedItems.TryAdd(rowIndexAndKey.Key, rowIndexAndKey.Value))
            {
                await Manager.SelectedItems.SendChangeNotificationAsync(new(orig, SelectedItems));
            }
        }
        else
        {
            await Manager.SelectedItems.SetValueAsync(new() { { rowIndexAndKey.Key, rowIndexAndKey.Value } });
        }
    }

    public virtual async Task RowDeselectHandlerAsync(RgfDynamicDictionary? rowData)
    {
        if (rowData != null && GridParameters.EnableMultiRowSelection == true)
        {
            var orig = SelectedItems.ToDictionary();
            var rowIndexAndKey = Manager.ListHandler.GetRowIndexAndKey(rowData);
            if (SelectedItems.Remove(rowIndexAndKey.Key))
            {
                await Manager.SelectedItems.SendChangeNotificationAsync(new(orig, SelectedItems));
            }
        }
        else
        {
            await Manager.SelectedItems.SetValueAsync(new());
        }
    }

    public virtual Task OnRecordDoubleClickAsync(RgfDynamicDictionary rowData)
    {
        var rowIndexAndKey = Manager.ListHandler.GetRowIndexAndKey(rowData);
        SelectedItems = new() { { rowIndexAndKey.Key, rowIndexAndKey.Value } };
        var eventArgs = new RgfEventArgs<RgfToolbarEventArgs>(this, new RgfToolbarEventArgs(Manager.SelectParam != null ? RgfToolbarEventKind.Select : RgfToolbarEventKind.Read));
        return EntityParameters.ToolbarParameters.EventDispatcher.DispatchEventAsync(eventArgs.Args.EventKind, eventArgs);
    }
}