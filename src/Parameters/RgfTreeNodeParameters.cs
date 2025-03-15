using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfTreeNodeParameters : IRgfTreeNodeParameters
{
    public int AbsoluteRowIndex { get; set; }

    public RgfDynamicDictionary RowData { get; set; } = [];

    public RgfProperty? Property { get; set; }

    public bool IsExpanded { get; set; }

    public List<RgfTreeNodeParameters>? Children { get; set; }

    public RenderFragment? EmbeddedGrid { get; set; }

    public RgfEntityParameters? EntityParameters { get; set; }

    public string? TooltipText { get; set; }
}