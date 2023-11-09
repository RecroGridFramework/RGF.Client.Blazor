using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Constants;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using System;
using System.Linq;
using System.Text;

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

    private RgfDynamicDialog _dynamicDialog { get; set; } = null!;

    public IRgManager? Manager { get; set; }

    private bool _initialized = false;

    private RgfEntityKey? FormDataKey { get; set; }

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
            EntityKey = EntityParameters.FormParameters?.EntityKey,
            ListParam = EntityParameters.ListParam,
            CustomParams = EntityParameters.CustomParams
        };

        Manager = new RgManager(param, _serviceProvider);
        Manager.RefreshEntity += Refresh;
        Manager.FormDataKey.OnAfterChange(this, OnChangeFormDataKey);
        Manager.NotificationManager.Subscribe<RgfUserMessage>(this, OnUserMessage);
        Manager.NotificationManager.Subscribe<RgfMenuEventArgs>(this, OnMenuCommanAsync);
        if (await ((RgManager)Manager).InitializeAsync(param, this.EntityParameters.FormOnly))
        {
            EntityParameters.Manager = Manager;
            _initialized = true;
        }
        else
        {
            _ = EntityParameters.DestroyEvent.InvokeAsync(EventArgs.Empty);
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
            StateHasChanged();
        });
    }

    protected virtual void OnUserMessage(IRgfEventArgs<RgfUserMessage> args)
    {
        if (args.Args.Origin == UserMessageOrigin.Global)
        {
            _dynamicDialog.Alert(args.Args.Title, args.Args.Message);
        }
    }

    protected virtual void OnChangeFormDataKey(ObservablePropertyEventArgs<RgfEntityKey?> args)
    {
        FormDataKey = args.NewData;
        EntityParameters.FormParameters!.EntityKey = FormDataKey ?? new();
        if (EntityParameters.FormOnly && FormDataKey == null)
        {
            _ = EntityParameters.DestroyEvent.InvokeAsync(EventArgs.Empty);
        }
    }

    protected virtual void OnMenuCommanAsync(IRgfEventArgs<RgfMenuEventArgs> args)
    {
        if (args.Args.Command == Menu.EntityEditor)
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
            param.DestroyEvent.Subscribe(e =>
            {
                _entityEditor = null;
                StateHasChanged();
            });
            _entityEditor = RgfEntityComponent.Create(param);
        }
    }

    public void Dispose()
    {
        if (Manager != null)
        {
            _logger.LogDebug("Manager.Dispose: {EntityName}", this.EntityName);
            Manager.Dispose();
            Manager = null;
        }
    }
}
