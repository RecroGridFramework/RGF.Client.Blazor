using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

[Obsolete("Use RgfComponentWrapper instead", true)]
public class DynamicComponentWrapper : RgfComponentWrapper { }

public partial class RgfComponentWrapper : ComponentBase
{
    [Inject]
    private ILogger<RgfComponentWrapper> _logger { get; set; } = null!;

    public RgfEventDispatcher<RgfWrapperEventKind, RgfWrapperEventArgs<RgfComponentWrapper>> EventDispatcher { get; } = new();

    private static int _nextId = 1;

    public static string GetNextId(string format = "rgf-id-{0}") => string.Format(format, _nextId++);

    private Dictionary<string, object> GetPropertiesDictionary(object? parameter)
    {
        var dictionary = new Dictionary<string, object>();
        if (parameter != null)
        {
            var properties = parameter.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            //var isComponent = parameter is IComponent;

            foreach (var propInfo in properties)
            {
                //if (!isComponent || propInfo.IsDefined(typeof(Microsoft.AspNetCore.Components.ParameterAttribute), inherit: false))
                {
                    var value = propInfo.GetValue(parameter);
                    if (value != null)
                    {
                        dictionary[propInfo.Name] = value;
                    }
                }
            }
        }
        if (ChildContent != null)
        {
            dictionary[nameof(ChildContent)] = ChildContent;
        }

        return dictionary;
    }

    internal static RenderFragment CreateDynamicComponent(Type componentType, string parameterName, object componentParameter, IRgManager manager)
    {
        var parameter = new Dictionary<string, object>() { { parameterName, componentParameter } };
        if (manager != null)
        {
            parameter.Add("Manager", manager);
        }
        return builder =>
        {
            int sequence = 0;
            builder.OpenComponent<DynamicComponent>(sequence++);
            builder.AddAttribute(sequence++, nameof(DynamicComponent.Type), componentType);
            builder.AddAttribute(sequence++, nameof(DynamicComponent.Parameters), parameter);
            builder.CloseComponent();
        };
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (OnComponentInitialized != null)
        {
            await OnComponentInitialized.Invoke(this);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _logger.LogDebug("OnAfterRender | ComponentType:{ComponentType}, FirstRender:{firstRender}", ComponentType, firstRender);

        await base.OnAfterRenderAsync(firstRender);

        var eventArgs = RgfWrapperEventArgs<RgfComponentWrapper>.CreateAfterRenderEvent(this, firstRender);
        await EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfWrapperEventArgs<RgfComponentWrapper>>(this, eventArgs));
    }
}