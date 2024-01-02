using Recrovit.RecroGridFramework.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfMenuParameters
{
    public int? MenuId { get; set; }

    public bool Navbar { get; set; } = false;

    public object? Icon { get; set; }

    public List<RgfMenu>? MenuItems { get; set; }

    public Func<RgfMenu, Task>? MenuSelectionCallback { get; set; }

    public Func<RgfMenu, Task>? MenuRenderCallback { get; set; }
}
