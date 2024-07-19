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

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfChartComponent : ComponentBase, IDisposable
{
    [Inject]
    private ILogger<RgfChartComponent> _logger { get; set; } = null!;

    [Inject]
    private IRecroDictService _recroDict { get; set; } = null!;

    public RgfChartParameters ChartParameters { get; private set; } = default!;

    public IRgfProperty[] ChartColumns { get; private set; } = [];

    public string[] DataColumns { get; set; } = [];

    public List<RgfDynamicDictionary> ChartData { get; set; } = [];

    private IRgManager Manager => EntityParameters.Manager!;

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    private EditContext _emptyEditContext = new(new object());

    private bool _showComponent { get; set; } = false;

    private RenderFragment? _chartDialog { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        EntityParameters.ToolbarParameters.MenuEventDispatcher.Subscribe(Menu.RecroChart, OnShowChart);
        ChartParameters = EntityParameters.ChartParameters;
        ChartParameters.DialogParameters.Title = "RecroChart";
        ChartParameters.DialogParameters.UniqueName = "chart-" + Manager.EntityDesc.NameVersion.ToLower();
        ChartParameters.DialogParameters.ShowCloseButton = true;
        ChartParameters.DialogParameters.ContentTemplate = ChartTemplate(this);
        ChartParameters.DialogParameters.FooterTemplate = FooterTemplate(this);
        ChartParameters.DialogParameters.Resizable = ChartParameters.DialogParameters.Resizable ?? true;
        ChartParameters.DialogParameters.Height = "560px";

        var validFormTypes = new[] {
            PropertyFormType.TextBox,
            PropertyFormType.TextBoxMultiLine,
            PropertyFormType.CheckBox,
            PropertyFormType.DropDown,
            PropertyFormType.Date,
            PropertyFormType.DateTime,
            PropertyFormType.StaticText
        };
        ChartColumns = Manager.EntityDesc.Properties.Where(p => p.Readable && validFormTypes.Contains(p.FormType)).OrderBy(e => e.ColTitle).ToArray();
    }

    private void OnShowChart(IRgfEventArgs<RgfMenuEventArgs> args)
    {
        args.Handled = true;
        Open();
    }

    private void Open()
    {
        ChartParameters.DialogParameters.OnClose = Close; //We'll reset it in case the dialog might have overwritten it
        if (EntityParameters.DialogTemplate != null)
        {
            _chartDialog = EntityParameters.DialogTemplate(ChartParameters.DialogParameters);
        }
        else
        {
            _chartDialog = RgfDynamicDialog.Create(ChartParameters.DialogParameters, _logger);
        }
        _showComponent = true;
        StateHasChanged();
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
        ChartData = new();
        _showComponent = false;
        ChartParameters.DialogParameters.Destroy?.Invoke();
        StateHasChanged();
        return true;
    }

    public virtual async Task<RgfChartDataResult> CreateChartDataAsyc(RgfChartParam chartParam)
    {
        ChartData = new();
        var res = await Manager.ListHandler.GetChartDataAsync(chartParam);
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
            DataColumns = res.Result.DataColumns;
            foreach (var item in res.Result.Data)
            {
                ChartData.Add(new RgfDynamicDictionary(DataColumns, item));
            }
        }
        return res.Result;
    }

    public void Dispose()
    {
        EntityParameters.ToolbarParameters.MenuEventDispatcher.Unsubscribe(Menu.RecroChart, OnShowChart);
    }
}