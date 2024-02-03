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

public partial class RgfDynamicDialog : ComponentBase
{
    [Inject]
    private IRecroDictService RecroDict { get; set; } = null!;

    public static RenderFragment Create(RgfDialogParameters parameters, ILogger? logger = null)
    {
        Type? type;
        if (!RgfBlazorConfiguration.ComponentTypes.TryGetValue(RgfBlazorConfiguration.ComponentType.Dialog, out type))
        {
            throw new NotImplementedException("The Dialog template component is missing.");
        }
        logger?.LogDebug("RgfDynamicDialog.Create");
        int build = 1;
        return builder =>
        {
            logger?.LogDebug("RgfDynamicDialog.Build:{build}", build++);
            int sequence = 0;
            builder.OpenComponent(sequence++, type);
            builder.AddAttribute(sequence++, "DialogParameters", parameters);
            builder.CloseComponent();
        };
    }

    private Dictionary<int, RenderFragment> _dynamicDialogs { get; set; } = new();

    private int _componentCount { get; set; } = 0;

    public void Info(string title, string message) => Dialog(DialogType.Info, title, message);

    public void Warning(string title, string message) => Dialog(DialogType.Warning, title, message);

    public void Alert(string title, string message) => Dialog(DialogType.Error, title, message);

    public void Dialog(DialogType dialogType, string title, string message) => Dialog(dialogType, title, (builder) => builder.AddContent(0, message));

    public void Dialog(RgfUserMessage message)
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
        Dialog(dialogType, message.Title, (builder) => builder.AddContent(0, message.Message));
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
        var key = ++_componentCount;
        parameters.OnClose = () =>
        {
            if (parameters.Destroy != null)
            {
                parameters.Destroy();
            }
            return Close(key);
        };
        parameters.PredefinedButtons = new List<ButtonParameters>() { new(RecroDict.GetRgfUiString("Close"), (arg) => parameters.OnClose(), true) };
        _dynamicDialogs.Add(_componentCount, Create(parameters));
        StateHasChanged();
    }

    public void Choice(string title, string message, IEnumerable<ButtonParameters> buttons, DialogType dialogType = DialogType.Default) => Choice(title, (builder) => builder.AddMarkupContent(0, message), buttons, dialogType);

    public void Choice(string title, RenderFragment content, IEnumerable<ButtonParameters> buttons, DialogType dialogType = DialogType.Default)
    {
        var key = ++_componentCount;
        RgfDialogParameters parameters = new()
        {
            Title = title,
            DialogType = dialogType,
            ShowCloseButton = false,
            ContentTemplate = content,
            OnClose = () => Close(key),
            PredefinedButtons = buttons
        };
        foreach (var item in buttons)
        {
            var handler = item.Callback;
            item.Callback = async (arg) =>
            {
                if (handler != null)
                {
                    await handler.Invoke(arg);
                }
                parameters.OnClose();
            };
        }
        _dynamicDialogs.Add(_componentCount, Create(parameters));
        StateHasChanged();
    }

    private bool Close(int key)
    {
        _dynamicDialogs.Remove(key);
        StateHasChanged();
        return true;
    }
}
