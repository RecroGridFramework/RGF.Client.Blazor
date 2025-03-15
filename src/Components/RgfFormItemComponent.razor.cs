using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfFormItemComponent : ComponentBase, IDisposable
{
    [Inject]
    internal ILogger<RgfFormItemComponent> _logger { get; set; } = null!;

    public RgfFormParameters FormParameters => BaseFormComponent.FormParameters;

    public string ErrorCssClass => FormParameters.ErrorCssClass ?? "";

    public string ModifiedCssClass => FormParameters.ModifiedCssClass ?? "";

    public string? CssClass
    {
        get
        {
            var css = $"{Property.CssClass ?? ""} {Property.RgClass}".Trim();
            return css.Length > 0 ? css : null;
        }
    }

    private RgfFormComponent BaseFormComponent => FormItemParameters.BaseFormComponent;

    public RenderFragment? ItemValidationMessage { get; private set; }

    private RgfForm.Property Property => FormItemParameters.Property;

    private TaskCompletionSource<bool> _firstRenderCompletion = new();

    public Task FirstRenderCompletionTask => _firstRenderCompletion.Task;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        BaseFormComponent.FormItemComponents.Add(this);

        CurrentEditContext.OnValidationStateChanged += (sender, args) => ItemValidationMessage = this.CreateValidationMessage(ErrorCssClass);
        //CurrentEditContext.OnValidationRequested += OnValidation;
        FormItemParameters.ItemData.OnAfterChange += (args) => { BaseFormComponent.FormValidation?.NotifyFieldChanged(FormItemParameters); };
    }

    protected override void OnAfterRender(bool firstRender)
    {
        _logger.LogDebug("OnAfterRender | Property:{Alias}, FirstRender:{firstRender}", Property.Alias, firstRender);

        base.OnAfterRender(firstRender);

        if (firstRender)
        {
            _firstRenderCompletion.SetResult(true);
        }
    }

    private void OnValidation(object? sender, ValidationRequestedEventArgs args)
    {
        //BaseFormComponent.FormValidation?.AddFieldError(_fieldId, $"Custom error");
    }

    public Task FindEntityAsync(string filter, bool formOnly = false)
    {
        var eventArgs = new RgfFormEventArgs(formOnly ? RgfFormEventKind.EntityDisplay : RgfFormEventKind.EntitySearch, BaseFormComponent, selectParam: this.CreateSelectParam(filter));
        return FormParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfFormEventArgs>(this, eventArgs));
    }

    public RenderFragment? CreateValidationMessage(string? cssClass = null)
    {
        RenderFragment? validationMessage = null;
        var messages = BaseFormComponent.CurrentEditContext.GetValidationMessages(FormItemParameters.FieldId).ToArray();
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
            StateHasChanged();//This is needed due to asynchronous validation
        }
        return validationMessage;
    }

    public RgfSelectParam CreateSelectParam(string filter)
    {
        RgfEntityKey current = new() { Keys = new() };
        var formData = BaseFormComponent.FormData;
        foreach (var item in Property.ForeignEntity.EntityKeys)
        {
            var clientName = $"rg-col-{item.Key}";
            var keyValue = formData.DataRec.GetMember(clientName);
            current.Keys.Add(clientName, keyValue);
        }
        var selectParam = new RgfSelectParam()
        {
            EntityName = Property.ForeignEntity.EntityName,
            ParentKey = formData.EntityKey,
            PropertyId = Property.Id,
            SelectedKeys = [current],
            Filter = new() { Keys = new RgfDynamicDictionary() { { $"rg-col-{Property.Id}", filter } } }
        };
        return selectParam;
    }

    public void Dispose()
    {
        BaseFormComponent.FormItemComponents.Remove(this);
    }
}