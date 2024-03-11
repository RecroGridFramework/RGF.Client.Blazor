using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Text.RegularExpressions;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfFormValidationComponent : ComponentBase
{
    [Inject]
    private IRecroDictService _recroDict { get; set; } = null!;

    public bool HasErrors { get; set; }

    public bool IsValid => !CurrentEditContext.GetValidationMessages().Any();

    private IRgManager Manager => BaseFormComponent.Manager;

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
        CurrentEditContext.SetFieldCssClassProvider(new RgfFieldCssClassProvider(BaseFormComponent));
    }

    private void OnFieldChanged(in FieldIdentifier fieldIdentifier)
    {
        if (fieldIdentifier.Model is RgfDynamicData dynamicData)
        {
            CurrentEditContext.MarkAsUnmodified(fieldIdentifier);
            return;
        }
        BaseFormComponent._logger.LogDebug("OnFieldChanged: {FieldName}", fieldIdentifier.FieldName);
        string alias = fieldIdentifier.FieldName;
        var property = BaseFormComponent.FormData.FormTabs.SelectMany(e => e.Groups.SelectMany(g => g.Properties)).SingleOrDefault(e => e.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
        if (property != null)
        {
            if (!BaseFormComponent.FormHandler.IsModified(BaseFormComponent.FormData, property))
            {
                CurrentEditContext.MarkAsUnmodified(fieldIdentifier);
                BaseFormComponent._logger.LogDebug("MarkAsUnmodified: {FieldName}", fieldIdentifier.FieldName);
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
        BaseFormComponent._logger.LogDebug("OnValidationRequested: {FieldName}", fieldIdentifier.FieldName);
        RgfFormEventArgs eventArgs;
        if (string.IsNullOrEmpty(fieldIdentifier.FieldName))
        {
            _messageStore.Clear();
            var properties = BaseFormComponent.FormData.FormTabs.SelectMany(e => e.Groups.SelectMany(g => g.Properties));
            foreach (var property in properties)
            {
                var fid = new FieldIdentifier(BaseFormComponent.FormData.DataRec, property.Alias);
                RequiredValidator(fid, property);
            }
            eventArgs = new RgfFormEventArgs(RgfFormEventKind.ValidationRequested, BaseFormComponent);
        }
        else
        {
            var alias = fieldIdentifier.FieldName;
            var property = BaseFormComponent.FormData.FormTabs.SelectMany(e => e.Groups.SelectMany(g => g.Properties).Where(e => e.Alias.Equals(alias))).SingleOrDefault();
            if (property != null)
            {
                _messageStore.Clear(fieldIdentifier);
                RequiredValidator(fieldIdentifier, property);
            }
            eventArgs = new RgfFormEventArgs(RgfFormEventKind.ValidationRequested, BaseFormComponent, fieldIdentifier, property);
        }
        _ = BaseFormComponent.FormParameters.EventDispatcher
            .DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfFormEventArgs>(this, eventArgs))
            .ContinueWith(t => CurrentEditContext.NotifyValidationStateChanged());
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
                    var data = BaseFormComponent.FormData.DataRec.GetItemData(property.Alias);
                    if (string.IsNullOrEmpty(data.ToString()))
                    {
                        var message = _recroDict.GetRgfUiString("FieldIsRequired");
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

    public void AddFieldError(string alias, string message, bool notifyValidationStateChanged = true) => AddFieldError(new FieldIdentifier(BaseFormComponent.FormData.DataRec, alias), message, notifyValidationStateChanged);

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
        _messageStore.Add(new FieldIdentifier(BaseFormComponent.FormData.DataRec, string.Empty), message);
        if (notifyValidationStateChanged)
        {
            CurrentEditContext.NotifyValidationStateChanged();
        }
    }

    public void NotifyFieldChanged(RgfFormItemParameters formItemParameters)
    {
        var property = formItemParameters.Property;
        BaseFormComponent._logger.LogDebug("NotifyFieldChanged: {Alias}", property.Alias);
        if (property.ForeignEntity?.EntityKeys.Any() == true)
        {
            //If the selector field is typed into, the previous key should be deleted
            var ek = property.ForeignEntity?.EntityKeys.First();
            var fkProp = Manager.EntityDesc.Properties.SingleOrDefault(e => e.Id == ek!.Foreign);
            if (fkProp != null)
            {
                this.BaseFormComponent.FormData.DataRec.Remove(fkProp.Alias);
            }
        }
        if (formItemParameters.ItemData.Value != null && formItemParameters.ItemData.ToString().Equals(string.Empty))
        {
            formItemParameters.ItemData.Value = null;
        }
        else
        {
            CurrentEditContext.NotifyFieldChanged(new FieldIdentifier(BaseFormComponent.FormData.DataRec, property.Alias));
        }
    }
}

public class RgfFieldCssClassProvider : FieldCssClassProvider
{
    public RgfFieldCssClassProvider(RgfFormComponent formComponent)
    {
        BaseFormComponent = formComponent;
    }

    public RgfFormComponent BaseFormComponent { get; }

    public override string GetFieldCssClass(EditContext editContext, in FieldIdentifier fieldIdentifier)
    {
        var fieldId = fieldIdentifier;
        if (fieldIdentifier.Model is RgfDynamicData dynamicData)
        {
            fieldId = new FieldIdentifier(editContext.Model, dynamicData.Name);
        }

        var formPar = BaseFormComponent.FormParameters;
        var cssClass = base.GetFieldCssClass(editContext, fieldId);
        var property = BaseFormComponent.FormData.FormTabs.SelectMany(e => e.Groups.SelectMany(g => g.Properties)).FirstOrDefault(e => e.Alias.Equals(fieldId.FieldName, StringComparison.OrdinalIgnoreCase));
        if (editContext.GetValidationMessages(fieldId).Any() ||
            BaseFormComponent.FormData.DataRec.GetMember(fieldId.FieldName) == null && property?.PropertyDesc.Editable == true && property?.PropertyDesc.Required == true)
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
        //BaseFormComponent._logger.LogDebug("GetFieldCssClass: {FieldName}, CssClass: {cssClass}", fieldId.FieldName, cssClass);
        return cssClass.Trim();
    }
}