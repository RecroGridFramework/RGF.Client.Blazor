using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfFormGroupLayoutParameters
{
    public RgfFormGroupLayoutParameters(RgfFormComponent formComponent, RgfForm.Group formGroup)
    {
        BaseFormComponent = formComponent;
        FormGroup = formGroup;
    }

    public RgfFormComponent BaseFormComponent { get; }

    public RgfForm.Group FormGroup { get; }
}
