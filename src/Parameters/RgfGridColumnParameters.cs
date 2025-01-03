﻿using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfGridColumnParameters
{
    public RgfGridColumnParameters(RgfDataComponentBase dataComponent, RgfProperty propDesc, RgfDynamicDictionary rowData)
    {
        DataComponentBase = dataComponent;
        PropDesc = propDesc;
        RowData = rowData;
    }

    public RgfDataComponentBase DataComponentBase { get; }

    public RgfProperty PropDesc { get; }

    public RgfDynamicDictionary RowData { get; }
}
