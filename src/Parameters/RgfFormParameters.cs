using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Events;
using Recrovit.RecroGridFramework.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;


public class RgfFormParameters
{
    public RgfEntityKey EntityKey { get; set; } = default!;

    public string? ErrorCssClass { get; set; }

    public string? ModifiedCssClass { get; set; }

    public RgfEventDispatcher<FormViewEventKind, RgfFormViewEventArgs> EventDispatcher { get; set; } = new();

    public Func<RgfFormComponent, bool, Task<RgfResult<RgfFormResult>>>? OnSaveAsync { get; set; }

    public RenderFragment<RgfFormGroupLayoutParameters>? FormGroupLayoutTemplate { get; set; }

    public RenderFragment<RgfFormItemParameters>? FormItemLayoutTemplate { get; set; }

    public RenderFragment<RgfFormItemParameters>? FormItemTemplate { get; set; }

    public RgfDialogParameters DialogParameters { get; set; } = new();
}
