using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;

namespace Recrovit.RecroGridFramework.Client.Blazor.Events;

public enum RgfGridEventKind
{
    CreateRowData,
    ColumnSettingsChanged
}

public class RgfGridEventArgs : EventArgs
{
    public RgfGridEventArgs(RgfGridEventKind eventKind, RgfGridComponent gridComponent, RgfDynamicDictionary? rowData = null, IEnumerable<RgfProperty>? properties = null)
    {
        EventKind = eventKind;
        BaseGridComponent = gridComponent;
        RowData = rowData;
        Properties = properties;
    }

    public RgfGridEventKind EventKind { get; }

    public RgfGridComponent BaseGridComponent { get; }

    public RgfDynamicDictionary? RowData { get; }

    public IEnumerable<RgfProperty>? Properties { get; }
}
