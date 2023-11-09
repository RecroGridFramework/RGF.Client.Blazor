using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Events;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfFormValidationComponent : ComponentBase
{
    public bool HasErrors { get; set; }

    public bool IsValid => !CurrentEditContext.GetValidationMessages().Any();

    private IRgManager Manager => FormComponent.Manager;

    private ValidationMessageStore _messageStore { get; set; } = null!;

    protected override void OnInitialized()
    {
        if (CurrentEditContext is null)
        {
            throw new InvalidOperationException(
                $"{nameof(RgfFormValidationComponent)} requires a cascading " +
                $"parameter of type {nameof(EditContext)}. " +
                $"For example, you can use {nameof(RgfFormValidationComponent)} " +
                $"inside an {nameof(EditForm)}.");
        }

        _messageStore = new(CurrentEditContext);

        CurrentEditContext.OnValidationStateChanged += (source, arg) => { HasErrors = CurrentEditContext.GetValidationMessages().Any(); StateHasChanged(); };
        CurrentEditContext.OnValidationRequested += (source, arg) => OnValidationRequested();
        CurrentEditContext.OnFieldChanged += (source, arg) => OnFieldChanged(arg.FieldIdentifier);
        CurrentEditContext.SetFieldCssClassProvider(new RgfFieldCssClassProvider(FormComponent));
    }

    private void OnFieldChanged(in FieldIdentifier fieldIdentifier)
    {
        if (fieldIdentifier.Model is RgfDynamicData dynamicData)
        {
            CurrentEditContext.MarkAsUnmodified(fieldIdentifier);
            return;
        }
        FormComponent._logger.LogDebug("OnFieldChanged: {FieldName}", fieldIdentifier.FieldName);
        string alias = fieldIdentifier.FieldName;
        var property = FormComponent.FormData.FormTabs.SelectMany(e => e.Groups.SelectMany(g => g.Properties)).SingleOrDefault(e => e.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
        if (property != null)
        {
            if (!FormComponent.FormHandler.IsModified(FormComponent.FormData, property))
            {
                CurrentEditContext.MarkAsUnmodified(fieldIdentifier);
                FormComponent._logger.LogDebug("MarkAsUnmodified: {FieldName}", fieldIdentifier.FieldName);
            }
            OnValidationRequested(fieldIdentifier);
        }
    }

    private void OnValidationRequested(in FieldIdentifier fieldIdentifier = default)
    {
        if (fieldIdentifier.Model is RgfDynamicData dynamicData)
        {
            return;
        }
        FormComponent._logger.LogDebug("OnValidationRequested: {FieldName}", fieldIdentifier.FieldName);
        var eventArgs = new RgfFormViewEventArgs(FormViewEventKind.ValidationRequested, FormComponent);
        if (string.IsNullOrEmpty(fieldIdentifier.FieldName))
        {
            _messageStore.Clear();
            var properties = FormComponent.FormData.FormTabs.SelectMany(e => e.Groups.SelectMany(g => g.Properties));
            foreach (var property in properties)
            {
                var fid = new FieldIdentifier(FormComponent.FormData.DataRec, property.Alias);
                RequiredValidator(fid, property);
            }
        }
        else
        {
            eventArgs.FieldId = fieldIdentifier;
            var alias = fieldIdentifier.FieldName;
            var property = FormComponent.FormData.FormTabs.SelectMany(e => e.Groups.SelectMany(g => g.Properties).Where(e => e.Alias.Equals(alias))).SingleOrDefault();
            if (property != null)
            {
                eventArgs.Property = property;
                _messageStore.Clear(fieldIdentifier);
                RequiredValidator(fieldIdentifier, property);
            }
        }
        FormComponent.FormParameters.EventDispatcher.DispatchEvent(eventArgs.EventKind, new RgfEventArgs<RgfFormViewEventArgs>(this, eventArgs));
        CurrentEditContext.NotifyValidationStateChanged();
    }

    private void RequiredValidator(FieldIdentifier fieldIdentifier, RgfForm.Property property)
    {
        switch (property.PropertyDesc.FormType)
        {
            case PropertyFormType.TextBox:
            case PropertyFormType.TextBoxMultiLine:
            case PropertyFormType.CheckBox:
            case PropertyFormType.DropDown:
            case PropertyFormType.Date:
            case PropertyFormType.DateTime:
            case PropertyFormType.HtmlEditor:
                if (property.PropertyDesc.Editable && property.PropertyDesc.Required)
                {
                    var data = FormComponent.FormData.DataRec.GetItemData(property.Alias);
                    if (string.IsNullOrEmpty(data.ToString()))
                    {
                        var message = Manager.RecroDict.GetRgfUiString("FieldIsRequired");
                        AddFieldError(fieldIdentifier, string.Format(message, property.Label), false);
                    }
                }
                break;
        }
    }

    public void ClearErrors()
    {
        _messageStore?.Clear();
        CurrentEditContext?.NotifyValidationStateChanged();
    }

    public void ReplaceFieldErrors(in FieldIdentifier fieldIdentifier, string message, bool notifyValidationStateChanged = true)
    {
        _messageStore.Clear(fieldIdentifier);
        AddFieldError(fieldIdentifier, message, notifyValidationStateChanged);
    }

    public void AddFieldError(string alias, string message, bool notifyValidationStateChanged = true) => AddFieldError(new FieldIdentifier(FormComponent.FormData.DataRec, alias), message, notifyValidationStateChanged);

    public void AddFieldError(in FieldIdentifier fieldIdentifier, string message, bool notifyValidationStateChanged = true)
    {
        _messageStore.Add(fieldIdentifier, message);
        if (notifyValidationStateChanged)
        {
            CurrentEditContext.NotifyValidationStateChanged();
        }
    }

    public void AddGlobalError(string message, bool notifyValidationStateChanged = true)
    {
        _messageStore.Add(new FieldIdentifier(FormComponent.FormData.DataRec, string.Empty), message);
        if (notifyValidationStateChanged)
        {
            CurrentEditContext.NotifyValidationStateChanged();
        }
    }

    public void NotifyFieldChanged(RgfFormItemParameters formItemParameters)
    {
        var property = formItemParameters.Property;
        FormComponent._logger.LogDebug("NotifyFieldChanged: {Alias}", property.Alias);
        if (property.ForeignEntity?.EntityKeys.Any() == true)
        {
            //If the selector field is typed into, the previous key should be deleted
            var ek = property.ForeignEntity?.EntityKeys.First();
            var fkProp = Manager.EntityDesc.Properties.SingleOrDefault(e => e.Id == ek!.Foreign);
            if (fkProp != null)
            {
                this.FormComponent.FormData.DataRec.Remove(fkProp.Alias);
            }
        }
        if (formItemParameters.ItemData.Value != null && formItemParameters.ItemData.ToString().Equals(string.Empty))
        {
            formItemParameters.ItemData.Value = null;
        }
        else
        {
            CurrentEditContext.NotifyFieldChanged(new FieldIdentifier(FormComponent.FormData.DataRec, property.Alias));
        }
    }
}

public class RgfFieldCssClassProvider : FieldCssClassProvider
{
    public RgfFieldCssClassProvider(RgfFormComponent formComponent)
    {
        FormComponent = formComponent;
    }

    public RgfFormComponent FormComponent { get; }

    public override string GetFieldCssClass(EditContext editContext, in FieldIdentifier fieldIdentifier)
    {
        var fieldId = fieldIdentifier;
        if (fieldIdentifier.Model is RgfDynamicData dynamicData)
        {
            fieldId = new FieldIdentifier(editContext.Model, dynamicData.Name);
        }

        var formPar = FormComponent.FormParameters;
        var cssClass = base.GetFieldCssClass(editContext, fieldId);
        var property = FormComponent.FormData.FormTabs.SelectMany(e => e.Groups.SelectMany(g => g.Properties)).FirstOrDefault(e => e.Alias.Equals(fieldId.FieldName, StringComparison.OrdinalIgnoreCase));
        if (editContext.GetValidationMessages(fieldId).Any() ||
            FormComponent.FormData.DataRec.GetMember(fieldId.FieldName) == null && property?.PropertyDesc.Editable == true && property?.PropertyDesc.Required == true)
        {
            cssClass = Regex.Replace(cssClass, @"\b" + Regex.Escape("valid") + @"\b", "");
            if (!cssClass.Contains("invalid"))
            {
                cssClass += " invalid";
            }
            if (!string.IsNullOrEmpty(formPar.ErrorCssClass) && !cssClass.Contains(formPar.ErrorCssClass))
            {
                cssClass = $"{cssClass} {formPar.ErrorCssClass}";
            }
        }
        if (!string.IsNullOrEmpty(formPar.ModifiedCssClass) && editContext.IsModified(fieldId))
        {
            cssClass = $"{cssClass} {formPar.ModifiedCssClass}";
        }
        //FormComponent._logger.LogDebug("GetFieldCssClass: {FieldName}, CssClass: {cssClass}", fieldId.FieldName, cssClass);
        return cssClass.Trim();
    }
}