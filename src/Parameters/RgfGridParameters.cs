using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfGridParameters
{
    public RenderFragment<RgfGridColumnParameters>? ColumnTemplate { get; set; }

    public RenderFragment<RgfGridComponent>? ColumnSettingsTemplate { get; set; }

    public GridEvents Events { get; } = new();
}
