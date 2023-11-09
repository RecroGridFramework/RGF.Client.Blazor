using Microsoft.AspNetCore.Components.Forms;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Client.Blazor.Parameters;

public class RgfFormItemParameters
{
    public RgfFormItemParameters(RgfFormComponent formComponent, RgfForm.Group group, RgfForm.Property property)
    {
        Group = group;
        FormComponent = formComponent;
        Property = property;
        ItemData = FormComponent.FormData.DataRec.GetItemData(Property.Alias);
        FieldId = new FieldIdentifier(FormComponent.FormData.DataRec, Property.Alias);
    }

    public RgfFormComponent FormComponent { get; }

    public RgfForm.Group Group { get; }

    public RgfForm.Property Property { get; }

    public FieldIdentifier FieldId { get; }

    public RgfDynamicData ItemData { get; }
}