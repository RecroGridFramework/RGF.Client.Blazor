using Microsoft.AspNetCore.Components.Forms;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;

namespace Recrovit.RecroGridFramework.Client.Blazor.Events;

public enum FormViewEventKind
{
    FormDataInitialized,
    ValidationRequested,
}

public class RgfFormViewEventArgs : EventArgs
{
    public RgfFormViewEventArgs(FormViewEventKind eventKind, RgfFormComponent formComponent)
    {
        EventKind = eventKind;
        BaseFormComponent = formComponent;
    }

    public FormViewEventKind EventKind { get; }

    public RgfFormComponent BaseFormComponent { get; }

    public FieldIdentifier? FieldId { get; internal set; }

    public RgfForm.Property? Property { get; internal set; }
}
