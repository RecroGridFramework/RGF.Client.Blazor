using Recrovit.RecroGridFramework.Abstraction.Models;
using System.Drawing;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfMenuParameters
{
    public int? MenuId { get; set; }

    public bool Navbar { get; set; }

    public object? Icon { get; set; }

    public bool? HideOnMouseLeave { get; set; }

    public List<RgfMenu>? MenuItems { get; set; }

    [Obsolete("Use OnMenuItemSelect instead")]
    public Func<RgfMenu, Task>? MenuSelectionCallback { get => OnMenuItemSelect; set => OnMenuItemSelect = value; }

    public Func<RgfMenu, Task>? OnMenuItemSelect { get; set; }

    [Obsolete("Use OnMenuRender instead")]
    public Func<RgfMenu, Task>? MenuRenderCallback { get => OnMenuRender; set => OnMenuRender = value; }

    public Func<RgfMenu, Task>? OnMenuRender { get; set; }

    public Func<bool>? OnMouseLeave { get; set; }

    public Point ContextMenuPosition { get; set; }
}