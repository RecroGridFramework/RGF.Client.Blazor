using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfChartParameters : RgfChartSettings
{
    public RgfDialogParameters DialogParameters { get; set; } = new();

    public RgfEventDispatcher<RgfChartEventKind, RgfChartEventArgs> EventDispatcher { get; } = new();
}