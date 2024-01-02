using Microsoft.AspNetCore.Components.Forms;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        FormComponent = formComponent;
    }

    public FormViewEventKind EventKind { get; }

    public RgfFormComponent FormComponent { get; }

    public FieldIdentifier? FieldId { get; internal set; }

    public RgfForm.Property? Property { get; internal set; }
}
