﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Handlers;
using System;
using System.Linq;
using System.Text;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfFormItemComponent : ComponentBase
{
    public string ErrorCssClass => FormItemParameters.FormComponent.FormParameters.ErrorCssClass ?? "";

    public string ModifiedCssClass => FormItemParameters.FormComponent.FormParameters.ModifiedCssClass ?? "";

    public string? CssClass
    {
        get
        {
            var css = $"{Property.CssClass ?? ""} {Property.RgClass}".Trim();
            return css.Length > 0 ? css : null;
        }
    }

    private IRgManager Manager => FormItemParameters.FormComponent.Manager;

    public RenderFragment? ItemValidationMessage { get; private set; }

    private RgfForm.Property Property => FormItemParameters.Property;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        CurrentEditContext.OnValidationStateChanged += (sender, args) => ItemValidationMessage = this.CreateValidationMessage(ErrorCssClass);
        //CurrentEditContext.OnValidationRequested += OnValidation;
        FormItemParameters.ItemData.OnAfterChange += (args) => { FormItemParameters.FormComponent.FormValidation?.NotifyFieldChanged(FormItemParameters); };
    }

    private void OnValidation(object? sender, ValidationRequestedEventArgs args)
    {
        //FormComponent.FormValidation?.AddFieldError(_fieldId, $"Custom error");
    }

    public void OnSearchEntity(string filter)
    {
        var selectParam = this.CreateSelectParam(filter);
        Manager.NotificationManager.RaiseEvent(selectParam, this);
    }

    public RenderFragment? CreateValidationMessage(string? cssClass = null)
    {
        RenderFragment? validationMessage = null;
        var messages = FormItemParameters.FormComponent.CurrentEditContext.GetValidationMessages(FormItemParameters.FieldId).ToArray();
        if (messages.Any())
        {
            validationMessage = (builder) =>
            {
                int sequence = 0;
                foreach (var message in messages)
                {
                    builder.OpenElement(sequence++, "div");
                    builder.AddAttribute(sequence++, "class", string.IsNullOrEmpty(cssClass) ? "validation-message" : $"validation-message {cssClass}");
                    //builder.AddMultipleAttributes(sequence++, AdditionalAttributes);
                    builder.AddContent(sequence++, message);
                    builder.CloseElement();
                }
            };
        }
        return validationMessage;
    }

    public RgfSelectParam CreateSelectParam(string filter)
    {
        RgfEntityKey current = new() { Keys = new() };
        var formData = FormItemParameters.FormComponent.FormData;
        foreach (var item in Property.ForeignEntity.EntityKeys)
        {
            var clientName = $"rg-col-{item.Key}";
            current.Keys.Add(clientName, formData.DataRec.GetMember(clientName));
        }
        var selectParam = new RgfSelectParam()
        {
            EntityName = Property.ForeignEntity.EntityName,
            ParentKey = formData.EntityKey,
            PropertyId = Property.Id,
            SelectedKey = current,
            Filter = new() { Keys = new RgfDynamicDictionary() { { $"rg-col-{Property.Id}", filter } } }
        };
        return selectParam;
    }
}
