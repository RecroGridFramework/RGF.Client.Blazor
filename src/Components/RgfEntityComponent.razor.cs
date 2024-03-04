using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Models;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfEntityComponent : ComponentBase, IDisposable
{
    public static RenderFragment Create(RgfEntityParameters entityParameters, ILogger? logger = null)
    {
        Type? type;
        if (string.IsNullOrEmpty(entityParameters.EntityName) ||
            !RgfBlazorConfiguration.EntityComponentTypes.TryGetValue(entityParameters.EntityName, out type))
        {
            type = RgfBlazorConfiguration.EntityComponentTypes[string.Empty];
        }
        return builder =>
        {
            logger?.LogDebug("RgfEntityComponent.Create => EntityName:{EntityName}, GridId:{GridId}", entityParameters.EntityName, entityParameters.GridId);
            int sequence = 0;
            builder.OpenComponent(sequence++, type);
            builder.AddAttribute(sequence++, nameof(RgfEntityComponent.EntityParameters), entityParameters);
            builder.CloseComponent();
        };
    }

    [Inject]
    private IServiceProvider _serviceProvider { get; set; } = null!;

    [Inject]
    private ILogger<RgfEntityComponent> _logger { get; set; } = null!;

    [Inject]
    private IJSRuntime _jsRuntime { get; set; } = default!;

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    public IRgManager? Manager { get; set; }

    private bool _initialized = false;

    private bool _showFormView { get; set; }

    private string? EntityName { get; set; }

    private RenderFragment? _entityEditor { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        EntityName = EntityParameters.EntityName;
        _logger.LogDebug("RgfEntityComponent.OnInitializedAsync: {EntityName}", EntityName);
        await CreateManagerAsync();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        _logger.LogDebug("RgfEntityComponent.OnParametersSetAsync: {EntityName}", EntityParameters.EntityName);
        if (EntityName != EntityParameters.EntityName)
        {
            EntityName = EntityParameters.EntityName;
            Refresh(true);
        }
        else
        {
            EntityParameters.Manager = Manager;
        }
    }

    private async Task CreateManagerAsync()
    {
        _logger.LogDebug("RgfEntityComponent.CreateManagerAsync");
        var param = new RgfGridRequest(this.EntityParameters)
        {
            EntityName = this.EntityParameters.EntityName,
            Skeleton = true,
            SelectParam = EntityParameters.SelectParam,
            EntityKey = EntityParameters.FormParameters?.FormViewKey.EntityKey,
            ListParam = EntityParameters.ListParam,
            CustomParams = EntityParameters.CustomParams
        };

        Manager = new RgManager(param, _serviceProvider);
        Manager.RefreshEntity += Refresh;
        Manager.FormViewKey.OnAfterChange(this, OnChangeFormViewKey);
        Manager.NotificationManager.Subscribe<RgfUserMessage>(this, OnUserMessage);
        EntityParameters.ToolbarParameters.EventDispatcher.Subscribe(RgfToolbarEventKind.EntityEditor, OnEntityEditorAsync, true);
        EntityParameters.ToolbarParameters.EventDispatcher.Subscribe(
            [RgfToolbarEventKind.Refresh, RgfToolbarEventKind.Add, RgfToolbarEventKind.Edit, RgfToolbarEventKind.Read, RgfToolbarEventKind.Delete, RgfToolbarEventKind.Select, RgfToolbarEventKind.SaveSettings, RgfToolbarEventKind.ResetSettings],
            Manager.OnToolbarCommandAsync, true);
        if (await ((RgManager)Manager).InitializeAsync(param, this.EntityParameters.FormOnly))
        {
            EntityParameters.Manager = Manager;
            await InitResourcesAsync();
            _initialized = true;
            var eventArgs = new RgfEntityEventArgs(RgfEntityEventKind.Initialized, Manager);
            await EntityParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfEntityEventArgs>(this, eventArgs));
            _logger.LogDebug("RgfEntityComponent.Initialized");
        }
        else
        {
            var eventArgs = new RgfEntityEventArgs(RgfEntityEventKind.Destroy, Manager);
            await EntityParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfEntityEventArgs>(this, eventArgs));
        }
    }

    private async Task InitResourcesAsync()
    {
        if (Manager?.EntityDesc.StylesheetsReferences?.Any() == true)
        {
            foreach (var css in Manager.EntityDesc.StylesheetsReferences)
            {
                await _jsRuntime.InvokeVoidAsync("Recrovit.LPUtils.AddStyleSheetLink", $"{ApiService.BaseAddress}{css}", false);
            }
        }
    }

    private void Refresh(bool recreate)
    {
        _initialized = false;
        StateHasChanged();
        _ = Task.Run(async () =>
        {
            if (recreate)
            {
                await CreateManagerAsync();
            }
            _initialized = true;
            _logger.LogDebug("RgfEntityComponent.Initialized");
            var eventArgs = new RgfEntityEventArgs(RgfEntityEventKind.Initialized, Manager!);
            await EntityParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfEntityEventArgs>(this, eventArgs));
            StateHasChanged();
        });
    }

    protected virtual void OnUserMessage(IRgfEventArgs<RgfUserMessage> args)
    {
        if (args.Args.Origin == UserMessageOrigin.Global)
        {
            _dynamicDialog.Dialog(args.Args);
        }
    }

    protected virtual void OnChangeFormViewKey(ObservablePropertyEventArgs<FormViewKey?> args)
    {
        _showFormView = args.NewData != null;
        EntityParameters.FormParameters.FormViewKey = args.NewData ?? new();
        if (EntityParameters.FormOnly && !_showFormView)
        {
            var eventArgs = new RgfEntityEventArgs(RgfEntityEventKind.Destroy, Manager!);
            _ = EntityParameters.EventDispatcher.DispatchEventAsync(eventArgs.EventKind, new RgfEventArgs<RgfEntityEventArgs>(this, eventArgs));
        }
    }

    private Task OnEntityEditorAsync(IRgfEventArgs<RgfToolbarEventArgs> args)
    {
        var param = new RgfEntityParameters("RecroGrid_Entity")
        {
            FormOnly = true,
            ListParam = new()
            {
                FixFilter = new RgfFilter.Condition[] {
                        new() { LogicalOperator = RgfFilter.LogicalOperator.And, PropertyId = 2, QueryOperator = RgfFilter.QueryOperator.Equal, IntValue1 = Manager?.EntityDesc.EntityId }
                    }
            }
        };
        param.EventDispatcher.Subscribe(RgfEntityEventKind.Destroy, (arg) =>
        {
            _entityEditor = null;
            StateHasChanged();
        });
        _entityEditor = RgfEntityComponent.Create(param);
        args.Handled = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        EntityParameters.ToolbarParameters.EventDispatcher.Unsubscribe(RgfToolbarEventKind.EntityEditor, OnEntityEditorAsync);
        if (Manager != null)
        {
            _logger.LogDebug("Manager.Dispose: {EntityName}", this.EntityName);
            EntityParameters.ToolbarParameters.EventDispatcher.Unsubscribe(
                [RgfToolbarEventKind.Refresh, RgfToolbarEventKind.Add, RgfToolbarEventKind.Edit, RgfToolbarEventKind.Read, RgfToolbarEventKind.Delete, RgfToolbarEventKind.Select, RgfToolbarEventKind.SaveSettings, RgfToolbarEventKind.ResetSettings],
                Manager.OnToolbarCommandAsync);
            Manager.Dispose();
            Manager = null;
        }
    }
}