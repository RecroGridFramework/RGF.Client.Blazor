﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Models;
using System.ComponentModel;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public enum FormEditMode
{
    Create,
    Update
}
public partial class RgfFormComponent : ComponentBase, IDisposable
{
    [Inject]
    internal ILogger<RgfFormComponent> _logger { get; set; } = null!;

    [Inject]
    public IJSRuntime JsRuntime { get; private set; } = null!;

    [Inject]
    private IRecroDictService _recroDict { get; set; } = null!;

    public RgfFormParameters FormParameters => EntityParameters.FormParameters;

    public EditContext CurrentEditContext { get; private set; } = default!;

    public IRgFormHandler FormHandler { get; private set; } = null!;

    public FormViewData FormData { get; private set; } = null!;

    public RgfFormValidationComponent? FormValidation { get; private set; }

    public FormEditMode FormEditMode { get; set; }

    public List<IDisposable> Disposables { get; private set; } = new();

    public IRgManager Manager { get => EntityParameters.Manager!; }

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    private RgfEntityKey? _previousEntityKey { get; set; }

    private bool ShowDialog { get; set; }

    private RenderFragment? _formDialog { get; set; }

    private RgfDialogParameters? _selectDialogParameters { get; set; }

    private RgfSelectParam? _selectParam { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        FormParameters.EventDispatcher.Subscribe(RgfFormEventKind.FindEntity, OnFindEntityAsync, true);
        Disposables.Add(Manager.NotificationManager.Subscribe<RgfUserMessage>(this, OnUserMessage));

        FormParameters.DialogParameters.CssClass = $"recro-grid-base rg-details {Manager.EntityDesc.NameVersion.ToLower()}";
        FormParameters.DialogParameters.UniqueName = Manager.EntityDesc.NameVersion.ToLower();
        FormParameters.DialogParameters.ContentTemplate = FormTemplate(this);
        FormParameters.DialogParameters.OnClose = OnClose;
        FormParameters.DialogParameters.Width = FormParameters.DialogParameters.Width;// ?? "80%";
        FormParameters.DialogParameters.Resizable = FormParameters.DialogParameters.Resizable ?? true;
        FormParameters.DialogParameters.NoHeader = FormParameters.DialogParameters.HeaderTemplate == null;
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        var key = FormParameters.FormViewKey.EntityKey;
        if (_previousEntityKey?.Equals(key) != true)
        {
            _previousEntityKey = key;
            ShowDialog = await this.ParametersSetAsync(key);
            if (ShowDialog)
            {
                if (EntityParameters.DialogTemplate != null)
                {
                    _formDialog = EntityParameters.DialogTemplate(FormParameters.DialogParameters);
                }
                else
                {
                    _formDialog = RgfDynamicDialog.Create(FormParameters.DialogParameters, _logger);
                }
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        var eventArg = new RgfEventArgs<RgfFormEventArgs>(this, RgfFormEventArgs.CreateAfterRenderEvent(this, firstRender));
        await FormParameters.EventDispatcher.DispatchEventAsync(eventArg.Args.EventKind, eventArg);
        _logger.LogDebug("OnAfterRender");
    }

    public Task FirstFormItemAsync() => SetFormItemAsync(FormParameters.FormViewKey.RowIndex == -1 ? -1 : 0);

    public Task LastFormItemAsync() => SetFormItemAsync(FormParameters.FormViewKey.RowIndex == -1 ? -1 : Manager.ItemCount.Value - 1);

    public Task NextFormItemAsync() => SetFormItemAsync(FormParameters.FormViewKey.RowIndex == -1 ? -1 : FormParameters.FormViewKey.RowIndex + 1);

    public Task PrevFormItemAsync() => SetFormItemAsync(FormParameters.FormViewKey.RowIndex == -1 ? -1 : FormParameters.FormViewKey.RowIndex - 1);

    private bool _setFormItemActive;

    private async Task SetFormItemAsync(int rowIndex, bool ignoreChanges = false)
    {
        if (rowIndex >= 0)
        {
            var basePermissions = Manager.ListHandler.CRUD;
            if (ignoreChanges == false &&
                (basePermissions.Create == false && FormEditMode == FormEditMode.Create || basePermissions.Update == false && FormEditMode == FormEditMode.Update))
            {
                ignoreChanges = true;
            }
            if (ignoreChanges || !CurrentEditContext.IsModified())
            {
                if (_setFormItemActive == false)
                {
                    try
                    {
                        _setFormItemActive = true;
                        var rowData = await Manager.ListHandler.EnsureVisibleAsync(rowIndex);
                        if (rowData != null)
                        {
                            await Manager.SelectedItems.SetValueAsync([rowData]);
                            var eventArgs = new RgfEventArgs<RgfToolbarEventArgs>(this, new RgfToolbarEventArgs(RgfToolbarEventKind.Read, rowData));
                            await EntityParameters.ToolbarParameters.EventDispatcher.DispatchEventAsync(eventArgs.Args.EventKind, eventArgs);
                        }
                    }
                    finally
                    {
                        _setFormItemActive = false;
                    }
                }
            }
            else
            {
                _dynamicDialog.Choice(
                    _recroDict.GetRgfUiString("UnsavedConfirmTitle"),
                    _recroDict.GetRgfUiString("UnsavedConfirm"),
                    [
                        new ButtonParameters(_recroDict.GetRgfUiString("Yes"), async (arg) =>
                    {
                        var success = await BeginSaveAsync(false);
                        if(success)
                        {
                            await SetFormItemAsync(rowIndex);
                        }
                    }),
                    new ButtonParameters(_recroDict.GetRgfUiString("No"), (arg) => SetFormItemAsync(rowIndex, true)),
                    new ButtonParameters(_recroDict.GetRgfUiString("Cancel"), isPrimary:true)
                    ],
                    DialogType.Warning);
            }
        }
    }

    public async Task<bool> ParametersSetAsync(RgfEntityKey entityKey)
    {
        FormEditMode = entityKey.IsEmpty ? FormEditMode.Create : FormEditMode.Update;
        if (FooterTemplate != null)
        {
            FormParameters.DialogParameters.FooterTemplate = FooterTemplate(this);
        }
        else
        {
            var basePermissions = Manager.ListHandler.CRUD;
            List<ButtonParameters> buttons = new();
            bool edit = basePermissions.Create && FormEditMode == FormEditMode.Create || basePermissions.Update && FormEditMode == FormEditMode.Update;
            if (edit)
            {
                buttons.Add(new(_recroDict.GetRgfUiString("Apply"), (arg) => BeginSaveAsync(false)) { ButtonName = "Apply" });
            }
            buttons.Add(new(_recroDict.GetRgfUiString(edit ? "Cancel" : "Close"), (arg) => OnClose()));
            if (edit)
            {
                buttons.Add(new("OK", (arg) => BeginSaveAsync(true), true));
            }
            FormParameters.DialogParameters.PredefinedButtons = buttons;
        }
        var eventArg = new RgfEventArgs<RgfFormEventArgs>(this, new RgfFormEventArgs(RgfFormEventKind.ParametersSet, this));
        await FormParameters.EventDispatcher.DispatchEventAsync(eventArg.Args.EventKind, eventArg);

        FormHandler = Manager.CreateFormHandler();
        var res = await FormHandler.InitializeAsync(entityKey);
        if (res.Success)
        {
            return await InitFormDataAsync(res.Result);
        }
        return false;
    }

    protected virtual void OnUserMessage(IRgfEventArgs<RgfUserMessage> args)
    {
        if (args.Args.Origin == UserMessageOrigin.FormView)
        {
            _dynamicDialog.Alert(args.Args.Title, args.Args.Message);
        }
    }

    public virtual RenderFragment GetFormGroupLayoutTemplate(RgfFormGroupLayoutParameters param)
    {
        if (FormParameters.FormGroupLayoutTemplate != null)
        {
            return FormParameters.FormGroupLayoutTemplate(param);
        }
        return FormGroupLayoutTemplate != null ? FormGroupLayoutTemplate(param) : DefaultFormGroupLayoutTemplate(param);
    }

    public virtual RenderFragment GetFormItemLayoutTemplate(RgfFormItemParameters param)
    {
        if (FormParameters.FormItemLayoutTemplate != null)
        {
            return FormParameters.FormItemLayoutTemplate(param);
        }
        return FormItemLayoutTemplate != null ? FormItemLayoutTemplate(param) : DefaultFormItemLayoutTemplate(param);
    }

    public virtual RenderFragment GetFormItemTemplate(RgfFormItemParameters param)
    {
        if (FormParameters.FormItemTemplate != null)
        {
            return FormParameters.FormItemTemplate(param);
        }
        return FormItemTemplate(param);
    }

    public virtual RenderFragment GetFormValidationTemplate()
    {
        return builder =>
        {
            int sequence = 0;
            builder.OpenComponent<RgfFormValidationComponent>(sequence++);
            builder.AddAttribute(sequence++, nameof(RgfFormValidationComponent.BaseFormComponent), this);
            builder.AddAttribute(sequence++, nameof(RgfFormValidationComponent.ChildContent), FormValidationTemplate(this));
            builder.AddComponentReferenceCapture(sequence++, (component) => FormValidation = (RgfFormValidationComponent)component);
            builder.CloseComponent();
        };
    }

    public void Close()
    {
        if (FormParameters.DialogParameters.Destroy != null)
        {
            FormParameters.DialogParameters.Destroy();
        }
        Manager.FormViewKey.Value = null;
    }

    public virtual bool OnClose()
    {
        var basePermissions = Manager.ListHandler.CRUD;
        var ignoreChanges = basePermissions.Create == false && FormEditMode == FormEditMode.Create || basePermissions.Update == false && FormEditMode == FormEditMode.Update;
        if (ignoreChanges == false && CurrentEditContext.IsModified())
        {
            _dynamicDialog.Choice(
                _recroDict.GetRgfUiString("UnsavedConfirmTitle"),
                _recroDict.GetRgfUiString("UnsavedConfirm"),
                [
                    new ButtonParameters(_recroDict.GetRgfUiString("Yes"), async (arg) => await BeginSaveAsync(true)),
                    new ButtonParameters(_recroDict.GetRgfUiString("No"), (arg) => Close()),
                    new ButtonParameters(_recroDict.GetRgfUiString("Cancel"), isPrimary:true)
                ],
                DialogType.Warning);

            return false;
        }
        Close();
        return true;
    }

    protected virtual Task OnFindEntityAsync(IRgfEventArgs<RgfFormEventArgs> arg)
    {
        _logger.LogDebug("OnFindEntity");
        _selectParam = arg.Args.SelectParam;
        if (_selectParam != null)
        {
            _selectParam.ItemSelectedEvent.Subscribe(OnGridItemSelected);
            _selectDialogParameters = new()
            {
                Resizable = true,
                ShowCloseButton = true,
                UniqueName = "select-" + Manager.EntityDesc.NameVersion.ToLower(),
                ContentTemplate = RgfEntityComponent.Create(new RgfEntityParameters(_selectParam.EntityName, Manager.SessionParams) { SelectParam = _selectParam }, _logger),
                OnClose = () => { OnGridItemSelected(new CancelEventArgs(true)); return true; },
            };
            _selectDialogParameters.PredefinedButtons = new List<ButtonParameters>() { new ButtonParameters(_recroDict.GetRgfUiString("Cancel"), (arg) => _selectDialogParameters.OnClose()) };
            FormParameters.DialogParameters.DynamicChild = EntityParameters.DialogTemplate != null ? EntityParameters.DialogTemplate(_selectDialogParameters) : RgfDynamicDialog.Create(_selectDialogParameters, _logger);
            StateHasChanged();
            arg.Handled = true;
        }
        return Task.CompletedTask;
    }

    protected virtual void OnGridItemSelected(CancelEventArgs args)
    {
        if (!args.Cancel)
        {
            this.ApplySelectParam(_selectParam!);
        }
        if (_selectDialogParameters?.Destroy != null)
        {
            _selectDialogParameters.Destroy();
        }
        FormParameters.DialogParameters.DynamicChild = null;
        _selectParam = null;
        _selectDialogParameters = null;
        StateHasChanged();
    }

    private async Task<bool> InitFormDataAsync(RgfFormResult formResult)
    {
        if (FormHandler?.InitFormData(formResult, out FormViewData? formData) == true && formData != null)
        {
            FormData = formData;
            if (!string.IsNullOrEmpty(FormData.StyleSheetUrl) && Manager.EntityDesc.Options.GetBoolValue("RGO_LegacyFormTemplate") != true)
            {
                await JsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", Services.ApiService.BaseAddress + FormData.StyleSheetUrl);
            }
            CurrentEditContext = new(FormData.DataRec);
            var eventArg = new RgfEventArgs<RgfFormEventArgs>(this, new RgfFormEventArgs(RgfFormEventKind.FormDataInitialized, this));
            await FormParameters.EventDispatcher.DispatchEventAsync(eventArg.Args.EventKind, eventArg);
            _logger.LogDebug("FormDataInitialized");
            return true;
        }
        return false;
    }

    public virtual async Task<bool> ValidateAsync()
    {
        if (FormValidation != null)
        {
            await FormValidation.ValidationRequestedAsync();
            return !CurrentEditContext.GetValidationMessages().Any();
        }

        return CurrentEditContext.Validate();
    }

    public virtual async Task<bool> BeginSaveAsync(bool close)
    {
        bool valid = await ValidateAsync();
        if (valid)
        {
            var res = await SaveAsync(!close);
            if (res.Success)
            {
                if (close)
                {
                    Close();
                }
                return true;
            }

            if (res.Messages?.Error != null)
            {
                foreach (var item in res.Messages.Error)
                {
                    if (item.Key.Equals(RgfCoreMessages.MessageDialog))
                    {
                        _dynamicDialog.Alert(_recroDict.GetRgfUiString("Error"), item.Value);
                    }
                    else
                    {
                        var prop = this.Manager.EntityDesc.Properties.FirstOrDefault(e => e.ClientName == item.Key);
                        if (prop != null)
                        {
                            FormValidation?.AddFieldError(prop.Alias, item.Value);
                        }
                        else
                        {
                            FormValidation?.AddFormError(item.Value);
                        }
                    }
                }
            }
        }
        return false;
    }

    public async Task<RgfResult<RgfFormResult>> SaveAsync(bool refresh)
    {
        RgfResult<RgfFormResult> res;
        if (FormParameters.OnSaveAsync != null)
        {
            _logger.LogDebug("OnSaveAsync => refresh:{refresh}", refresh);
            res = await FormParameters.OnSaveAsync.Invoke(this, refresh);
        }
        else
        {
            res = await FormHandler.SaveAsync(FormData, refresh);
        }
        if (res.Success)
        {
            if (FormEditMode == FormEditMode.Create)
            {
                FormEditMode = FormEditMode.Update;
            }
            if (refresh)
            {
                await InitFormDataAsync(res.Result);
            }
            if (res.Messages != null)
            {
                Manager.BroadcastMessages(res.Messages, this);
            }
        }
        return res;
    }

    public virtual Task<RgfResult<RgfFormResult>> OnSaveAsync(bool refresh) => FormHandler.SaveAsync(FormData, refresh);

    public virtual void ApplySelectParam(RgfSelectParam param)
    {
        var prop = FormData.FormTabs.SelectMany(e => e.Groups.SelectMany(g => g.Properties).Where(e => e.Id == param.PropertyId)).SingleOrDefault();
        if (prop != null)
        {
            var filter = param.Filter.Keys.First();
            var key = param.SelectedKey.Keys.First();
            var foreign = prop.ForeignEntity.EntityKeys.First().Foreign;
            var keyProp = Manager.EntityDesc.Properties.SingleOrDefault(e => e.Id == foreign);

            _logger.LogDebug("ApplySelectParam => filter:{alias}={value}, key:{alias}={value}", prop.Alias, filter.Value, keyProp?.Alias, key.Value);

            FormData.DataRec.SetMember(prop.Alias, filter.Value);
            if (keyProp?.Alias != null)
            {
                FormData.DataRec.SetMember(keyProp.Alias, key.Value);
            }
        }
    }

    public virtual void DisposeFormComponent()
    {
        FormParameters.EventDispatcher.Unsubscribe(RgfFormEventKind.FindEntity, OnFindEntityAsync);
        if (Disposables != null)
        {
            Disposables.ForEach(disposable => disposable.Dispose());
            Disposables = null!;
        }
        if (!string.IsNullOrEmpty(FormData.StyleSheetUrl))
        {
            JsRuntime.InvokeVoidAsync("Recrovit.LPUtils.RemoveLinkedFile", Services.ApiService.BaseAddress + FormData.StyleSheetUrl, "stylesheet");
        }
        if (FormHandler != null)
        {
            FormHandler.Dispose();
            FormHandler = null!;
        }
    }

    public void Dispose()
    {
        ShowDialog = false;
        Manager.FormViewKey.Value = null;
        this.DisposeFormComponent();
    }
}