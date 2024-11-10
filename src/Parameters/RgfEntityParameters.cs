using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfEntityParameters : RgfSessionParams
{
    public RgfEntityParameters(string entityName, RgfSessionParams? sessionParam = null) : base(sessionParam)
    {
        EntityName = entityName;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the initialization process
    /// is deferred and will not run automatically. When set to true, 
    /// the initialization must be manually triggered at a later time.
    /// </summary>
    public bool DeferredInitialization { get; set; }

    public IRgManager? Manager { get; internal set; }

    public IRgManager? ParentManager { get; internal set; }

    public string EntityName { get; }

    public RgfSelectParam? SelectParam { get; set; }

    public RenderFragment<RgfDialogParameters>? DialogTemplate { get; set; }

    public RgfToolbarParameters ToolbarParameters { get; set; } = new();

    public RgfGridParameters GridParameters { get; set; } = new();

    public RgfFilterParameters FilterParameters { get; set; } = new();

    public RgfPagerParameters PagerParameters { get; set; } = new();

    public RgfFormParameters FormParameters { get; set; } = new();

    public RgfChartParameters ChartParameters { get; set; } = new();

    public RenderFragment<IRgManager>? TitleTemplate { get; set; }

    public bool FormOnly { get; set; }

    public RgfListParam? ListParam { get; set; }

    public Dictionary<string, object>? CustomParams { get; set; }

    public RgfEventDispatcher<RgfEntityEventKind, RgfEntityEventArgs> EventDispatcher { get; } = new();
}