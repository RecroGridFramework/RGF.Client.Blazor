using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Events;
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

    public RgfFormParameters FormParameters { get; private set; } = default!;

    public EditContext CurrentEditContext { get; private set; } = default!;

    public IRgFormHandler FormHandler { get; private set; } = null!;

    public FormViewData FormData { get; private set; } = null!;

    public RgfFormValidationComponent? FormValidation { get; private set; }

    public FormEditMode FormEditMode { get; set; }

    public List<IDisposable> Disposables { get; private set; } = new();

    public IRgManager Manager { get => EntityParameters.Manager!; }

    private IRecroDictService RecroDict => Manager.RecroDict;

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    private bool RemoveStyleSheet { get; set; }//TODO: This needs to be reconsidered => RemoveStyleSheet

    private RgfEntityKey? _previousEntityKey { get; set; }

    private bool ShowDialog { get; set; }

    private RenderFragment? _formDialog { get; set; }

    private RgfDialogParameters? _selectDialogParameters { get; set; }

    private RgfSelectParam? _selectParam { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Disposables.Add(Manager.NotificationManager.Subscribe<RgfSelectParam>(this, OnSelectGrid));
        Disposables.Add(Manager.NotificationManager.Subscribe<RgfUserMessage>(this, OnUserMessage));

        FormParameters = EntityParameters.FormParameters;
        FormParameters.DialogParameters.CssClass = $"recro-grid-base rg-details {Manager.EntityDesc.NameVersion.ToLower()}";
        FormParameters.DialogParameters.UniqueName = Manager.EntityDesc.NameVersion.ToLower();
        FormParameters.DialogParameters.ContentTemplate = FormTemplate(this);
        FormParameters.DialogParameters.OnClose = OnClose;
        FormParameters.DialogParameters.Width = FormParameters.DialogParameters.Width ?? "80%";
        FormParameters.DialogParameters.Resizable = FormParameters.DialogParameters.Resizable ?? true;
        FormParameters.DialogParameters.NoHeader = FormParameters.DialogParameters.HeaderTemplate == null;

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
                buttons.Add(new(RecroDict.GetRgfUiString("Apply"), (arg) => BeginSaveAsync(false)));
            }
            buttons.Add(new(RecroDict.GetRgfUiString(edit ? "Cancel" : "Close"), (arg) => OnClose()));
            if (edit)
            {
                buttons.Add(new("OK", (arg) => BeginSaveAsync(true), true));
            }
            FormParameters.DialogParameters.PredefinedButtons = buttons;
        }
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

    public Task FirstFormItemAsync() => SetFormItemAsync(FormParameters.FormViewKey.RowIndex == -1 ? -1 : 0);

    public Task LastFormItemAsync() => SetFormItemAsync(FormParameters.FormViewKey.RowIndex == -1 ? -1 : Manager.ItemCount.Value - 1);

    public Task NextFormItemAsync() => SetFormItemAsync(FormParameters.FormViewKey.RowIndex == -1 ? -1 : FormParameters.FormViewKey.RowIndex + 1);

    public Task PrevFormItemAsync() => SetFormItemAsync(FormParameters.FormViewKey.RowIndex == -1 ? -1 : FormParameters.FormViewKey.RowIndex - 1);

    private bool _setFormItemActive;

    private async Task SetFormItemAsync(int rowIndex, bool ignoreChanges = false)
    {
        if (rowIndex >= 0)
        {
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
                            await Manager.NotificationManager.RaiseEventAsync(new RgfToolbarEventArgs(ToolbarAction.Read, rowData), this);
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
                    RecroDict.GetRgfUiString("UnsavedConfirmTitle"),
                    RecroDict.GetRgfUiString("UnsavedConfirm"),
                    [
                        new ButtonParameters(RecroDict.GetRgfUiString("Yes"), async (arg) =>
                    {
                        var success = await BeginSaveAsync(false);
                        if(success)
                        {
                            await SetFormItemAsync(rowIndex);
                        }
                    }),
                    new ButtonParameters(RecroDict.GetRgfUiString("No"), (arg) => SetFormItemAsync(rowIndex, true)),
                    new ButtonParameters(RecroDict.GetRgfUiString("Cancel"), isPrimary:true)
                    ],
                    DialogType.Warning);
            }
        }
    }

    public async Task<bool> ParametersSetAsync(RgfEntityKey entityKey)
    {
        FormEditMode = entityKey.IsEmpty ? FormEditMode.Create : FormEditMode.Update;
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
        if (CurrentEditContext.IsModified())
        {
            _dynamicDialog.Choice(
                RecroDict.GetRgfUiString("UnsavedConfirmTitle"),
                RecroDict.GetRgfUiString("UnsavedConfirm"),
                [
                    new ButtonParameters(RecroDict.GetRgfUiString("Yes"), async (arg) => await BeginSaveAsync(true)),
                    new ButtonParameters(RecroDict.GetRgfUiString("No"), (arg) => Close()),
                    new ButtonParameters(RecroDict.GetRgfUiString("Cancel"), isPrimary:true)
                ],
                DialogType.Warning);

            return false;
        }
        Close();
        return true;
    }

    protected virtual void OnSelectGrid(IRgfEventArgs<RgfSelectParam> args)
    {
        _logger.LogDebug("RgfFormComponent.OnSelectGrid");
        _selectParam = args.Args;
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
            _selectDialogParameters.PredefinedButtons = new List<ButtonParameters>() { new ButtonParameters(RecroDict.GetRgfUiString("Cancel"), (arg) => _selectDialogParameters.OnClose()) };
            FormParameters.DialogParameters.DynamicChild = EntityParameters.DialogTemplate != null ? EntityParameters.DialogTemplate(_selectDialogParameters) : RgfDynamicDialog.Create(_selectDialogParameters, _logger);
        }
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
            if (!RemoveStyleSheet && !string.IsNullOrEmpty(FormData.StyleSheetUrl))
            {
                //RemoveStyleSheet = await JsRuntime.InvokeAsync<bool>("Recrovit.LPUtils.AddStyleSheetLink", ApiService.BaseAddress + _formViewData.StyleSheetUrl);
            }
            CurrentEditContext = new(FormData.DataRec);
            var eventArgs = new RgfFormEventArgs(RgfFormEventKind.FormDataInitialized, this);
            await FormParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfFormEventArgs>(this, eventArgs));
            _logger.LogDebug("FormDataInitialized");
            return true;
        }
        return false;
    }

    public virtual bool Validate() => CurrentEditContext.Validate();

    public virtual async Task<bool> BeginSaveAsync(bool close)
    {
        if (Validate())
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
                    if (item.Key.Equals(RgfMessages.MessageDialog))
                    {
                        _dynamicDialog.Alert(RecroDict.GetRgfUiString("Error"), item.Value);
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
                            FormValidation?.AddGlobalError(item.Value);
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
        if (Disposables != null)
        {
            Disposables.ForEach(disposable => disposable.Dispose());
            Disposables = null!;
        }
        if (RemoveStyleSheet)
        {
            JsRuntime.InvokeVoidAsync("Recrovit.LPUtils.RemoveLinkedFile", Services.ApiService.BaseAddress + FormData.StyleSheetUrl, "stylesheet");
            RemoveStyleSheet = false;
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