using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfFilterComponent : ComponentBase, IDisposable
{
    [Inject]
    private ILogger<RgfFilterComponent> _logger { get; set; } = null!;

    [Inject]
    private IRecroDictService _recroDict { get; set; } = null!;

    public RgfFilterParameters FilterParameters { get; private set; } = default!;

    public RgfFilter.Condition? Condition { get; private set; }

    public RgfPredefinedFilter PredefinedFilter { get; private set; } = new();

    public string PredefinedFilterName { get; set; } = string.Empty;

    public bool IsPredefinedFilterAdmin => Manager.EntityDesc.Permissions.GetPermission(RgfPermissionType.PredefFilterAdmin);

    public RgfFilterProperty[] RgfFilterProperties => FilterHandler.RgfFilterProperties;

    public void AddCondition(RgfFilter.Condition condition) => FilterHandler.AddCondition(_logger, condition.ClientId);
    public void RemoveCondition(RgfFilter.Condition condition) => FilterHandler.RemoveCondition(condition.ClientId);
    public void AddBracket(RgfFilter.Condition condition) => FilterHandler.AddBracket(condition.ClientId);
    public void RemoveBracket(RgfFilter.Condition condition) => FilterHandler.RemoveBracket(condition.ClientId);
    public bool ChangeProperty(RgfFilter.Condition condition, int newPropertyId) => FilterHandler.ChangeProperty(condition, newPropertyId);
    public bool ChangeQueryOperator(RgfFilter.Condition condition, RgfFilter.QueryOperator newOperator) => FilterHandler.ChangeQueryOperator(_logger, condition, newOperator);


    private EditContext _emptyEditContext = new(new object());

    public bool IsFilterActive { get; set; } = true;

    private IRgManager Manager => EntityParameters.Manager!;

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    private bool _showComponent { get; set; } = false;

    private RenderFragment? _filterDialog { get; set; }

    public IRgFilterHandler FilterHandler { get; private set; } = null!;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        EntityParameters.ToolbarParameters.EventDispatcher.Subscribe(RgfToolbarEventKind.ShowFilter, OnShowFilter, true);

        FilterParameters = EntityParameters.FilterParameters;
        FilterParameters.DialogParameters.Title = _recroDict.GetRgfUiString("Filter");
        FilterParameters.DialogParameters.UniqueName = "filter-" + Manager.EntityDesc.NameVersion.ToLower();
        FilterParameters.DialogParameters.ShowCloseButton = true;
        FilterParameters.DialogParameters.ContentTemplate = FilterTemplate(this);
        FilterParameters.DialogParameters.FooterTemplate = FooterTemplate(this);
        FilterParameters.DialogParameters.Resizable = FilterParameters.DialogParameters.Resizable ?? true;
    }

    protected virtual async Task OnShowFilter(IRgfEventArgs<RgfToolbarEventArgs> args)
    {
        _logger.LogDebug("RgfFilter.ShowFilter");
        FilterHandler = await Manager.GetFilterHandlerAsync();
        Condition = new RgfFilter.Condition() { Conditions = FilterHandler.Conditions };
        IsFilterActive = Manager.IsFiltered || !FilterHandler.Conditions.Any();
        Open();
        args.Handled = true;
    }

    private void Open()
    {
        FilterParameters.DialogParameters.OnClose = Close; //We'll reset it in case the dialog might have overwritten it
        if (EntityParameters.DialogTemplate != null)
        {
            _filterDialog = EntityParameters.DialogTemplate(FilterParameters.DialogParameters);
        }
        else
        {
            _filterDialog = RgfDynamicDialog.Create(FilterParameters.DialogParameters, _logger);
        }
        _showComponent = true;
        StateHasChanged();
    }

    public void OnClose(MouseEventArgs? args)
    {
        if (FilterParameters.DialogParameters.OnClose != null)
        {
            FilterParameters.DialogParameters.OnClose();
        }
        else
        {
            Close();
        }
    }

    private bool Close()
    {
        _logger.LogDebug("RgfFilter.Close");
        _showComponent = false;
        FilterParameters.DialogParameters.Destroy?.Invoke();
        StateHasChanged();
        return true;
    }

    public void Recreate()
    {
        OnClose(null);
        _ = Task.Run(() =>
        {
            Open();
            StateHasChanged();
        });
    }

    public virtual void OnCancel(MouseEventArgs? args)
    {
        PredefinedFilterName = string.Empty;
        FilterHandler.ResetFilter();
        OnClose(args);
    }

    public virtual async Task OnOk(MouseEventArgs args)
    {
        _logger.LogDebug("RgfFilter.OnOk");
        _showComponent = false;
        OnClose(args);
        var conditions = IsFilterActive ? FilterHandler.StoreFilter() : new RgfFilter.Condition[] { };
        await Manager.ListHandler.SetFilterAsync(conditions, PredefinedFilter.QueryTimeout);
    }

    public virtual void OnSetPredefinedFilter(string key)
    {
        _logger.LogDebug("SetPredefinedFilter: {key}", key);
        var predefFilter = FilterHandler.SelectPredefinedFilter(key);
        if (predefFilter != null)
        {
            PredefinedFilter = predefFilter;
            Condition = new RgfFilter.Condition() { Conditions = FilterHandler.Conditions };
        }
        else
        {
            PredefinedFilter = new()
            {
                Name = key,
                QueryTimeout = PredefinedFilter.QueryTimeout
            };
        }
    }

    public virtual async Task OnSavePredefinedFilter()
    {
        if (await FilterHandler.SavePredefinedFilterAsync(PredefinedFilter))
        {
            StateHasChanged();
        }
    }

    public virtual void OnDeletePredefinedFilter()
    {
        _dynamicDialog.Choice(
            _recroDict.GetRgfUiString("Delete"),
            _recroDict.GetRgfUiString("DelConfirm"),
            new List<ButtonParameters>()
            {
                new ButtonParameters(_recroDict.GetRgfUiString("Yes"), async (arg) => {
                    if (await FilterHandler.SavePredefinedFilterAsync(PredefinedFilter, true))
                    {
                        PredefinedFilterName = string.Empty;
                        StateHasChanged();
                    }
                }),
                new ButtonParameters(_recroDict.GetRgfUiString("No"), isPrimary:true)
            },
            DialogType.Warning);
    }

    public void Dispose()
    {
        EntityParameters.ToolbarParameters.EventDispatcher.Unsubscribe(RgfToolbarEventKind.ShowFilter, OnShowFilter);
    }
}