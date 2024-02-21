using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfGridColumnParameters
{
    public RgfGridColumnParameters(RgfGridComponent gridComponent, RgfProperty propDesc, RgfDynamicDictionary rowData)
    {
        BaseGridComponent = gridComponent;
        PropDesc = propDesc;
        RowData = rowData;
    }

    public RgfGridComponent BaseGridComponent { get; }

    public RgfProperty PropDesc { get; }

    public RgfDynamicDictionary RowData { get; }
}
