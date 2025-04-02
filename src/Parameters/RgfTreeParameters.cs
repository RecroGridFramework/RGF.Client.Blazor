using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfTreeParameters
{
    public RgfEventDispatcher<RgfTreeEventKind, RgfTreeEventArgs> EventDispatcher { get; } = new();
}