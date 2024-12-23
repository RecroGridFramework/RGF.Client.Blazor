using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfToolbarParameters
{
    public bool QuickFilterEnabled { get; set; }

    public RgfEventDispatcher<RgfToolbarEventKind, RgfToolbarEventArgs> EventDispatcher { get; } = new();

    public RgfEventDispatcher<string, RgfMenuEventArgs> MenuEventDispatcher { get; } = new();
}