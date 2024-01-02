using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfFormGroupLayoutParameters
{
    public RgfFormGroupLayoutParameters(RgfFormComponent formComponent, RgfForm.Group formGroup)
    {
        FormComponent = formComponent;
        FormGroup = formGroup;
    }

    public RgfFormComponent FormComponent { get; }

    public RgfForm.Group FormGroup { get; }
}
