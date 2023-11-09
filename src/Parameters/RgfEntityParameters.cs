using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Handlers;
using System;
using System.Linq;
using System.Text;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfEntityParameters : RgfSessionParams
{
    public RgfEntityParameters(string entityName, RgfSessionParams? sessionParam = null) : base(sessionParam)
    {
        EntityName = entityName;
    }

    public IRgManager? Manager { get; internal set; }

    public string EntityName { get; }

    public RgfSelectParam? SelectParam { get; set; }

    public RenderFragment<RgfDialogParameters>? DialogTemplate { get; set; }

    public RgfToolbarParameters ToolbarParameters { get; set; } = new();

    public RgfGridParameters GridParameters { get; set; } = new();

    public RgfFilterParameters FilterParameters { get; set; } = new();

    public RgfPagerParameters PagerParameters { get; set; } = new();

    public RgfFormParameters FormParameters { get; set; } = new();

    public RenderFragment<IRgManager>? TitleTemplate { get; set; }

    public bool FormOnly { get; set; }

    public RgfListParam? ListParam { get; set; }

    public Dictionary<string, object>? CustomParams { get; set; }

    public EventDispatcher<EventArgs> DestroyEvent { get; } = new();
}
