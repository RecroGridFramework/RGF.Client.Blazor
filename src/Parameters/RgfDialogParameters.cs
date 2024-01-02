using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Recrovit.RecroGridFramework.Client.Blazor.Components;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfDialogParameters
{
    public string? Title { get; set; }

    public bool IsModal { get; set; } = true;

    public DialogType DialogType { get; set; } = DialogType.Default;

    public bool? ShowCloseButton { get; set; }

    public bool? Resizable { get; set; }

    public string? Width { get; set; }

    public string? Height { get; set; }

    public string? CssClass { get; set; }

    public string? UniqueName { get; set; }

    public RenderFragment? HeaderTemplate { get; set; }

    public RenderFragment? ContentTemplate { get; set; }

    public RenderFragment? FooterTemplate { get; set; }

    public RenderFragment? DynamicChild { get; set; }

    public Func<bool>? OnClose { get; set; }

    public Action? Destroy { get; set; }

    public IEnumerable<ButtonParameters>? PredefinedButtons { get; set; }

    public bool NoHeader { get; set; } = default!;

    public RenderFragment Header
    {
        get
        {
            if (!string.IsNullOrEmpty(Title))
            {
                return builder => builder.AddContent(0, Title);
            }
            if (HeaderTemplate != null)
            {
                return HeaderTemplate;
            }
            return null!;
        }
    }

    public RenderFragment Content => ContentTemplate!;
}

public class ButtonParameters
{
    public ButtonParameters() { Callback = (arg) => Task.CompletedTask; }
    public ButtonParameters(string? childText, Action<MouseEventArgs>? callback, bool isPrimary = false) : this(childText, (arg) => { callback?.Invoke(arg); return Task.CompletedTask; }, isPrimary) { }
    public ButtonParameters(string? childText, Func<MouseEventArgs, Task>? callbackAsync = null, bool isPrimary = false)
    {
        ChildText = childText;
        Callback = callbackAsync ?? ((arg) => Task.CompletedTask);
        IsPrimary = isPrimary;
    }

    public string? ChildText { get; set; }

    public bool IsPrimary { get; set; } = false;

    public RenderFragment? ChildContent { get; set; }

    public Func<MouseEventArgs, Task> Callback { get; set; }
}