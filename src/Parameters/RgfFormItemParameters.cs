using Microsoft.AspNetCore.Components.Forms;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfFormItemParameters
{
    public RgfFormItemParameters(RgfFormComponent formComponent, RgfForm.Group group, RgfForm.Property property)
    {
        Group = group;
        BaseFormComponent = formComponent;
        Property = property;
        ItemData = BaseFormComponent.FormData.DataRec.GetItemData(Property.Alias);
        FieldId = new FieldIdentifier(BaseFormComponent.FormData.DataRec, Property.Alias);
    }

    public RgfFormComponent BaseFormComponent { get; }

    public RgfForm.Group Group { get; }

    public RgfForm.Property Property { get; }

    public FieldIdentifier FieldId { get; }

    public RgfDynamicData ItemData { get; }
}