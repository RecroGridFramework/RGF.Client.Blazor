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

    public string? MinWidth { get; set; }

    public string? CssClass { get; set; }

    public string? UniqueName { get; set; }

    public RenderFragment? HeaderTemplate { get; set; }

    public RenderFragment? ContentTemplate { get; set; }

    public RenderFragment? FooterTemplate { get; set; }

    public RenderFragment? DynamicChild { get; set; }

    public Func<bool>? OnClose { get; set; }

    public Action? Destroy { get; set; }

    public IEnumerable<ButtonParameters>? PredefinedButtons { get; set; }

    public IEnumerable<ButtonParameters>? LeftButtons { get; set; }

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

    public RenderFragment Content => ContentTemplate ?? ((builder) => builder.AddContent(1, ""));
}

public class ButtonParameters
{
    public ButtonParameters() { Callback = (arg) => Task.CompletedTask; }

    public ButtonParameters(string iconName, string title, Action<MouseEventArgs>? callback, string? cssClass = null) : this(null, (arg) => { callback?.Invoke(arg); return Task.CompletedTask; }, cssClass: cssClass, iconName: iconName, title: title) { }

    public ButtonParameters(string iconName, string title, Func<MouseEventArgs, Task>? callbackAsync = null, string? cssClass = null) : this(null, callbackAsync, cssClass: cssClass, iconName: iconName, title: title) { }

    public ButtonParameters(string? childText, Action<MouseEventArgs>? callback, bool isPrimary = false, string? cssClass = null) : this(childText, (arg) => { callback?.Invoke(arg); return Task.CompletedTask; }, isPrimary, cssClass: cssClass) { }

    public ButtonParameters(string? childText, Func<MouseEventArgs, Task>? callbackAsync = null, bool isPrimary = false, string? cssClass = null, string? iconName = null, string? minWidth = null, string? title = null)
    {
        ChildText = childText;
        Title = title;
        Callback = callbackAsync ?? ((arg) => Task.CompletedTask);
        IsPrimary = isPrimary;
        CssClass = cssClass;
        IconName = iconName;
        MinWidth = minWidth ?? (iconName == null ? "4.5rem" : null);
    }

    public string? ButtonName { get; set; }

    public string? ChildText { get; set; }

    public string? CssClass { get; set; }

    public string? IconName { get; set; }

    public string? Title { get; set; }

    public bool IsPrimary { get; set; } = false;

    public bool Disabled { get; set; }

    public string? MinWidth { get; set; }

    public RenderFragment? ChildContent { get; set; }

    public Func<MouseEventArgs, Task> Callback { get; set; }
}