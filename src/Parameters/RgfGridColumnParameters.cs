using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfGridColumnParameters
{
    public RgfGridColumnParameters(RgfGridComponent gridComponent, RgfProperty propDesc, RgfDynamicDictionary rowData)
    {
        GridComponent = gridComponent;
        PropDesc = propDesc;
        RowData = rowData;
    }

    public RgfGridComponent GridComponent { get; }

    public RgfProperty PropDesc { get; }

    public RgfDynamicDictionary RowData { get; }
}
