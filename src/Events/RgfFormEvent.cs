using Microsoft.AspNetCore.Components.Forms;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;

namespace Recrovit.RecroGridFramework.Client.Blazor.Events;

public enum RgfFormEventKind
{
    FormDataInitialized,
    ValidationRequested,
}

public class RgfFormEventArgs : EventArgs
{
    public RgfFormEventArgs(RgfFormEventKind eventKind, RgfFormComponent formComponent)
    {
        EventKind = eventKind;
        BaseFormComponent = formComponent;
    }

    public RgfFormEventKind EventKind { get; }

    public RgfFormComponent BaseFormComponent { get; }

    public FieldIdentifier? FieldId { get; internal set; }

    public RgfForm.Property? Property { get; internal set; }
}
