using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Events;
using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfGridParameters
{
    public RenderFragment<RgfGridColumnParameters>? ColumnTemplate { get; set; }

    public RenderFragment<RgfGridComponent>? ColumnSettingsTemplate { get; set; }

    public RgfEventDispatcher<RgfGridEventKind, RgfGridEventArgs> EventDispatcher { get; } = new();
}
