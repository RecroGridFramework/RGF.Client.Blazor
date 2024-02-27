using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Events;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Models;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfFormParameters
{
    public FormViewKey FormViewKey { get; internal set; } = new();

    public string? ErrorCssClass { get; set; }

    public string? ModifiedCssClass { get; set; }

    public RgfEventDispatcher<RgfFormEventKind, RgfFormEventArgs> EventDispatcher { get; } = new();

    public Func<RgfFormComponent, bool, Task<RgfResult<RgfFormResult>>>? OnSaveAsync { get; set; }

    public RenderFragment<RgfFormGroupLayoutParameters>? FormGroupLayoutTemplate { get; set; }

    public RenderFragment<RgfFormItemParameters>? FormItemLayoutTemplate { get; set; }

    public RenderFragment<RgfFormItemParameters>? FormItemTemplate { get; set; }

    public RgfDialogParameters DialogParameters { get; set; } = new();
}
