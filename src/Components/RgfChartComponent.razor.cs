using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Collections.Concurrent;
using System.Data;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfChartComponent : ComponentBase, IDisposable
{
    [Inject]
    private ILogger<RgfChartComponent> _logger { get; set; } = null!;

    [Inject]
    private IRecroDictService _recroDict { get; set; } = null!;

    [Inject]
    private IRecroSecService _recroSec { get; set; } = null!;

    private ConcurrentDictionary<string, string> _recroDictChart = [];

    public string GetRecroDictChart(string stringId, string? defaultValue = null) => _recroDict.GetItem(_recroDictChart, stringId, defaultValue);

    public List<RgfDynamicDictionary> DataColumns { get; set; } = [];

    public List<RgfDynamicDictionary> ChartData { get; set; } = [];

    public RenderFragment? EmbeddedGrid { get; set; }

    private RgfEntityParameters EmbeddedGridEntityParameters = null!;

    private IRgManager Manager => EntityParameters.Manager!;

    private RgfChartParameters ChartParameters => EntityParameters.ChartParameters;

    public IEnumerable<RgfProperty> AllowedProperties { get; private set; } = [];

    public Dictionary<int, string> ChartColumnsNumeric => AllowedProperties.Where(e => e.ListType == PropertyListType.Numeric || e.ClientDataType.IsNumeric()).OrderBy(e => e.ColTitle).ToDictionary(p => p.Id, p => p.ColTitle);

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    private bool _showComponent = true;

    public bool IsStateValid { get; set; }

    private RenderFragment? _chartDialog { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _recroDictChart = await _recroDict.GetDictionaryAsync("RGF.UI.Chart", _recroSec.UserLanguage);

        var validFormTypes = new[] {
            PropertyFormType.TextBox,
            PropertyFormType.TextBoxMultiLine,
            PropertyFormType.CheckBox,
            PropertyFormType.DropDown,
            PropertyFormType.Date,
            PropertyFormType.DateTime,
            PropertyFormType.StaticText
        };
        AllowedProperties = Manager.EntityDesc.Properties.Where(p => p.Readable && !p.IsDynamic && validFormTypes.Contains(p.FormType)).OrderBy(e => e.ColTitle).ToArray();

        EntityParameters.ToolbarParameters.MenuEventDispatcher.Subscribe(Menu.RecroChart, OnShowChart);
        EntityParameters.ToolbarParameters.EventDispatcher.Subscribe(RgfToolbarEventKind.RecroChart, OnShowChart);

        ChartParameters.DialogParameters.Title = "RecroChart - " + Manager.EntityDesc.MenuTitle;
        ChartParameters.DialogParameters.UniqueName = "chart-" + Manager.EntityDesc.NameVersion.ToLower();
        ChartParameters.DialogParameters.OnClose = Close;
        ChartParameters.DialogParameters.ShowCloseButton = true;
        ChartParameters.DialogParameters.ContentTemplate = ContentTemplate(this);
        ChartParameters.DialogParameters.FooterTemplate = FooterTemplate(this);
        ChartParameters.DialogParameters.Resizable ??= true;
        ChartParameters.DialogParameters.Height = "560px";
        ChartParameters.DialogParameters.MinWidth = "600px";

        if (EntityParameters.DialogTemplate != null)
        {
            _chartDialog = EntityParameters.DialogTemplate(ChartParameters.DialogParameters);
        }
        else
        {
            _chartDialog = RgfDynamicDialog.Create(ChartParameters.DialogParameters, _logger);
        }

        var req = new RgfGridRequest(Manager.SessionParams, "RGRecroChart");
        EmbeddedGridEntityParameters = new RgfEntityParameters(req.EntityName, Manager.SessionParams) { GridRequest = req, DeferredInitialization = true };
        EmbeddedGrid = RgfEntityComponent.Create(EmbeddedGridEntityParameters);
    }

    private void OnShowChart(IRgfEventArgs args)
    {
        //ChartParameters.DialogParameters.OnClose = Close; //We'll reset it in case the dialog might have overwritten it
        _showComponent = true;
        args.Handled = true;
        args.PreventDefault = true;
        StateHasChanged();
        var eventArgs = new RgfChartEventArgs(RgfChartEventKind.ShowChart);
        _ = EntityParameters.ChartParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfChartEventArgs>(this, eventArgs));
    }

    public void OnClose(MouseEventArgs? args)
    {
        if (ChartParameters.DialogParameters.OnClose != null)
        {
            ChartParameters.DialogParameters.OnClose();
        }
        else
        {
            Close();
        }
    }

    private bool Close()
    {
        _showComponent = false;
        ChartParameters.DialogParameters.Destroy?.Invoke();
        StateHasChanged();
        return true;
    }

    public virtual async Task<bool> CreateChartDataAsyc(RgfAggregationSettings aggregationSettings)
    {
        var req = Manager.ListHandler.CreateAggregateRequest(aggregationSettings);
        req.EntityName = "RGRecroChart";
        ChartData = [];
        DataColumns = [];

        var chartManager = EmbeddedGridEntityParameters.Manager!;

        await chartManager.RecreateAsync(req);

        var chartDataColumns = chartManager.ListHandler.DataColumns;
        var dataList = await chartManager.ListHandler.GetDataRangeAsync(0, chartManager.ListHandler.ItemCount.Value - 1);

        foreach (var item in aggregationSettings.Columns)
        {
            string alias;
            if (item.Aggregate == "Count")
            {
                alias = "Count";
            }
            else
            {
                var oprop = AllowedProperties.FirstOrDefault(e => e.Id == item.PropertyId);
                if (oprop == null)
                {
                    continue;
                }
                alias = $"{oprop.Alias}_{item.Aggregate}";
            }
            var prop = chartManager.EntityDesc.Properties.FirstOrDefault(e => e.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
            if (prop == null)
            {
                continue;
            }
            int idx = Array.FindIndex(chartDataColumns, e => e == prop.ClientName);
            if (idx < 0)
            {
                continue;
            }
            var dataCol = new RgfDynamicDictionary();
            dataCol.SetMember("Aggregate", item.Aggregate);
            dataCol.SetMember("Alias", prop.Alias);
            dataCol.SetMember("Index", idx);
            var name = item.Aggregate == "Count" ? _recroDict.GetRgfUiString("ItemCount") : prop.ColTitle;
            dataCol.SetMember("Name", name);
            DataColumns.Add(dataCol);
        }

        var order = new List<string>();
        foreach (var propertyId in aggregationSettings.Groups.Concat(aggregationSettings.SubGroup))
        {
            var oprop = AllowedProperties.FirstOrDefault(e => e.Id == propertyId);
            if (oprop == null)
            {
                continue;
            }
            string alias = oprop.Alias;
            var prop = chartManager.EntityDesc.Properties.FirstOrDefault(e => e.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
            if (prop == null)
            {
                continue;
            }
            int idx = Array.FindIndex(chartDataColumns, e => e == prop.ClientName);
            if (idx < 0)
            {
                continue;
            }
            var dataCol = new RgfDynamicDictionary();
            dataCol.SetMember("Alias", prop.Alias);
            dataCol.SetMember("PropertyId", propertyId);
            dataCol.SetMember("Index", idx);
            dataCol.SetMember("Name", prop.ColTitle);
            DataColumns.Add(dataCol);
            order.Add(prop.Alias);
        }

        IOrderedEnumerable<RgfDynamicDictionary>? ordered = null;
        foreach (var item in order)
        {
            if (ordered == null)
            {
                ordered = dataList.OrderBy(e => e.GetMember(item)?.ToString());
            }
            else
            {
                ordered = ordered.ThenBy(e => e.GetMember(item)?.ToString());
            }
        }
        ChartData = ordered?.ToList() ?? dataList;
        IsStateValid = true;

        return true;
    }

    public void Validation(ValidationMessageStore messageStore, RgfChartSettings rgfChartSettings)
    {
        messageStore.Clear();
        var aggregationSettings = rgfChartSettings.AggregationSettings;
        for (int i = aggregationSettings.Columns.Count - 1; i >= 0; i--)
        {
            var col = aggregationSettings.Columns[i];
            if (col.Aggregate == "Count")
            {
                col.PropertyId = 0;
                for (int i2 = 0; i2 < i; i2++)
                {
                    if (aggregationSettings.Columns[i2].Aggregate == "Count")
                    {
                        messageStore.Add(() => col.Aggregate, "");
                    }
                }
            }
            else if (col.PropertyId == 0)
            {
                messageStore.Add(() => col.PropertyId, "");
            }
        }
        if (rgfChartSettings.SeriesType != RgfChartSeriesType.Bar && rgfChartSettings.SeriesType != RgfChartSeriesType.Line)
        {
            if (aggregationSettings.Columns.Count > 1)
            {
                messageStore.Add(() => aggregationSettings.Columns[1], "");
            }
            if (aggregationSettings.SubGroup.Count > 0)
            {
                messageStore.Add(() => aggregationSettings.SubGroup[0], "");
            }
        }
        for (int i = aggregationSettings.SubGroup.Count - 1; i >= 0; i--)
        {
            int id = aggregationSettings.SubGroup[i];
            if (id == 0 || aggregationSettings.SubGroup.IndexOf(id) < i || aggregationSettings.Groups.IndexOf(id) != -1)
            {
                messageStore.Add(() => aggregationSettings.SubGroup[i], "");
            }
        }
        for (int i = aggregationSettings.Groups.Count - 1; i >= 0; i--)
        {
            int id = aggregationSettings.Groups[i];
            if (id == 0 || aggregationSettings.Groups.IndexOf(id) < i)
            {
                messageStore.Add(() => aggregationSettings.Groups[i], "");
            }
        }
    }

    public void SetState(bool valid)
    {
        IsStateValid = valid;
        StateHasChanged();
    }

    public void AddColumn()
    {
        IsStateValid = false;
        ChartParameters.AggregationSettings.Columns.Add(new() { PropertyId = 0, Aggregate = "Sum" });
    }

    public void RemoveColumn(RgfAggregationColumn column)
    {
        IsStateValid = false;
        ChartParameters.AggregationSettings.Columns.Remove(column);
    }

    public void AddGroup()
    {
        IsStateValid = false;
        ChartParameters.AggregationSettings.Groups.Add(0);
    }

    public void RemoveAtGroup(int idx)
    {
        IsStateValid = false;
        ChartParameters.AggregationSettings.Groups.RemoveAt(idx);
    }

    public void AddSubGroup()
    {
        IsStateValid = false;
        ChartParameters.AggregationSettings.SubGroup.Add(0);
    }

    public void RemoveAtSubGroup(int idx)
    {
        IsStateValid = false;
        ChartParameters.AggregationSettings.SubGroup.RemoveAt(idx);
    }

    public void Dispose()
    {
        EntityParameters.ToolbarParameters.MenuEventDispatcher.Unsubscribe(Menu.RecroChart, OnShowChart);
        EntityParameters.ToolbarParameters.EventDispatcher.Unsubscribe(RgfToolbarEventKind.RecroChart, OnShowChart);
    }
}