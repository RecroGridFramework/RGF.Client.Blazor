using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public enum DialogType
{
    Default = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

[Flags]
public enum ApprovalType
{
    None = 0,
    Yes = 1 << 0,   // 1
    No = 1 << 1,    // 2
    Cancel = 1 << 2,// 4
    All = 1 << 3    // 8
}

public partial class RgfDynamicDialog : ComponentBase
{
    [Inject]
    private IRecroDictService RecroDict { get; set; } = null!;

    public static RenderFragment Create(RgfDialogParameters parameters, ILogger? logger = null, Func<RgfComponentWrapper, Task>? onComponentInitialized = null)
    {
        Type type = RgfBlazorConfiguration.GetComponentType(RgfBlazorConfiguration.ComponentType.Dialog);
        logger?.LogDebug("Create | {DialogType}:{UniqueName}, {Title}", parameters.DialogType, parameters.UniqueName, parameters.Title);
        int build = 1;
        return builder =>
        {
            logger?.LogDebug("Build | Build:{build}, {DialogType}:{UniqueName}, {Title}", build++, parameters.DialogType, parameters.UniqueName, parameters.Title);
            int sequence = 0;
            builder.OpenComponent<RgfComponentWrapper>(sequence++);
            builder.AddAttribute(sequence++, nameof(RgfComponentWrapper.OnComponentInitialized), onComponentInitialized);
            builder.AddAttribute(sequence++, nameof(RgfComponentWrapper.ChildContent), (RenderFragment)(childBuilder =>
            {
                int childSequence = 0;
                childBuilder.OpenComponent(childSequence++, type);
                childBuilder.AddAttribute(childSequence++, "DialogParameters", parameters);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        };
    }

    private Dictionary<int, RenderFragment> _dynamicDialogs { get; set; } = [];

    public int DialogCount => _dynamicDialogs.Count;

    private int _dialogKeyCounter { get; set; } = 0;

    public void Info(string title, string message) => Dialog(DialogType.Info, title, message);

    public void Warning(string title, string message) => Dialog(DialogType.Warning, title, message);

    public void Alert(string title, string message) => Dialog(DialogType.Error, title, message);

    public void Dialog(DialogType dialogType, string title, string message) => Dialog(dialogType, title, (builder) => builder.AddContent(0, message));

    public void Dialog(RgfUserMessageEventArgs message)
    {
        DialogType dialogType;
        switch (message.Category)
        {
            case UserMessageType.Information:
                dialogType = DialogType.Info;
                break;
            case UserMessageType.Warning:
                dialogType = DialogType.Warning;
                break;
            case UserMessageType.Error:
                dialogType = DialogType.Error;
                break;
            default:
                dialogType = DialogType.Default;
                break;
        }
        Dialog(dialogType, message.Title, (builder) => builder.AddMarkupContent(0, message.Message));
    }

    public void Dialog(DialogType dialogType, string title, RenderFragment content)
    {
        RgfDialogParameters parameters = new()
        {
            Title = title,
            DialogType = dialogType,
            ShowCloseButton = true,
            ContentTemplate = content,
        };
        Dialog(parameters);
    }

    public void Dialog(RgfDialogParameters parameters)
    {
        var key = ++_dialogKeyCounter;
        parameters.EventDispatcher.Subscribe(RgfDialogEventKind.Destroy, (args) => Destroy(key), true);
        if (parameters.PredefinedButtons == null)
        {
            parameters.PredefinedButtons = [new(RecroDict.GetRgfUiString("Close"), (arg) => parameters.EventDispatcher.RaiseEventAsync(RgfDialogEventKind.Close, this), isPrimary: true)];
        }
        _dynamicDialogs.Add(_dialogKeyCounter, Create(parameters));
        StateHasChanged();
    }

    public void Choice(string? title, string message, IEnumerable<ButtonParameters> buttons, DialogType dialogType = DialogType.Default) => Choice(title, (builder) => builder.AddMarkupContent(0, message), buttons, dialogType);

    public void PromptDeletionConfirmation(Func<Task> deleteAction, string? titleSuffix = null) =>
        PromptDeletionConfirmation(ApprovalType.Yes | ApprovalType.No, async (approval) =>
        {
            if (approval == ApprovalType.Yes)
            {
                await deleteAction();
            }
        }, titleSuffix);

    public void PromptDeletionConfirmation(ApprovalType availableOptions, Func<ApprovalType, Task> deleteAction, string? titleSuffix = null, ApprovalType primary = ApprovalType.No)
    {
        var title = RecroDict.GetRgfUiString("Delete");
        if (!string.IsNullOrEmpty(titleSuffix))
        {
            title += $" - {titleSuffix}";
        }
        PromptActionConfirmation(title, RecroDict.GetRgfUiString("DelConfirm"), availableOptions, deleteAction, DialogType.Warning, primary);
    }

    public void PromptActionConfirmation(string? title, string confirmationMessage, ApprovalType availableOptions, Func<ApprovalType, Task> action, DialogType dialogType = DialogType.Warning, ApprovalType primary = ApprovalType.Cancel)
    {
        var buttons = new List<ButtonParameters>();
        void AddButton(ApprovalType type, string label)
        {
            if ((availableOptions & type) == type)
            {
                buttons.Add(new(RecroDict.GetRgfUiString(label), (arg) => action(type), type == primary));
            }
        }

        AddButton(ApprovalType.Yes, "Yes");
        AddButton(ApprovalType.No, "No");
        AddButton(ApprovalType.All, "All");
        AddButton(ApprovalType.Cancel, "Cancel");

        Choice(title, confirmationMessage, buttons, dialogType);
    }

    public Task<ApprovalType> PromptActionConfirmationAsync(string? title, string confirmationMessage, ApprovalType availableOptions, DialogType dialogType = DialogType.Warning, ApprovalType primary = ApprovalType.Cancel)
    {
        var _taskCompletionSource = new TaskCompletionSource<ApprovalType>();

        PromptActionConfirmation(title, confirmationMessage, availableOptions, (arg) =>
        {
            _taskCompletionSource?.SetResult(arg);
            _taskCompletionSource = null;
            return Task.CompletedTask;
        }, dialogType, primary);

        return _taskCompletionSource.Task;
    }

    public void Choice(string? title, RenderFragment content, IEnumerable<ButtonParameters> buttons, DialogType dialogType = DialogType.Default)
    {
        if (string.IsNullOrEmpty(title))
        {
            title = dialogType switch
            {
                DialogType.Info => RecroDict.GetRgfUiString("Information"),
                DialogType.Warning => RecroDict.GetRgfUiString("Warning"),
                DialogType.Error => RecroDict.GetRgfUiString("Error"),
                _ => title
            };
        }
        var key = ++_dialogKeyCounter;
        RgfDialogParameters parameters = new()
        {
            Title = title,
            DialogType = dialogType,
            ShowCloseButton = false,
            ContentTemplate = content,
            PredefinedButtons = buttons
        };
        parameters.EventDispatcher.Subscribe(RgfDialogEventKind.Destroy, (args) => Destroy(key), true);

        foreach (var item in buttons)
        {
            var handler = item.Callback;
            item.Callback = async (arg) =>
            {
                if (handler != null)
                {
                    await handler.Invoke(arg);
                }
                await parameters.EventDispatcher.RaiseEventAsync(RgfDialogEventKind.Close, this);
            };
        }
        _dynamicDialogs.Add(_dialogKeyCounter, Create(parameters));
        StateHasChanged();
    }

    private void Destroy(int key)
    {
        _dynamicDialogs.Remove(key);
        StateHasChanged();
    }
}