﻿using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfTreeComponent : RgfDataComponentBase
{
    [Inject]
    private ILogger<RgfTreeComponent> _logger { get; set; } = default!;

    private List<RgfProperty>? _gridProperties = null;

    private Dictionary<int, RgfTreeNodeParameters> _nodeCache = [];

    private List<RgfProperty> GridTypeProperties => _gridProperties ??= Manager.EntityDesc.Properties.Where(e => e.FormType == PropertyFormType.RecroGrid && e.FormTab > 0).OrderBy(e => $"{e.FormTab}/{e.FormGroup}/{e.FormPos}").ToList();

    private int? _treeViewColumnCount;

    public int TreeViewColumnCount() => _treeViewColumnCount ??= Manager.EntityDesc.Options.GetIntValue("RGO_TreeViewColumnCount", 1);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        EntityParameters.ToolbarParameters.EventDispatcher.Subscribe(RgfToolbarEventKind.Refresh, OnRefresh);
        GridDataSource.OnAfterChange(this, (args) => { OnRefresh(null); });
    }

    private void OnRefresh(IRgfEventArgs<RgfToolbarEventArgs>? args)
    {
        _nodeCache.Clear();
        StateHasChanged();
    }

    public RgfTreeNodeParameters GetNodeParameters(RgfDynamicDictionary rowData)
    {
        int absoluteRowIndex = Manager.ListHandler.GetAbsoluteRowIndex(rowData);
        RgfTreeNodeParameters? node;
        if (_nodeCache.TryGetValue(absoluteRowIndex, out node))
        {
            return node;
        }
        node = new RgfTreeNodeParameters()
        {
            RowData = rowData,
            AbsoluteRowIndex = absoluteRowIndex,
            EntityParameters = EntityParameters,
            Property = Manager.EntityDesc.SortedVisibleColumns.FirstOrDefault()
        };
        node.Children = [];
        foreach (var prop in this.GridTypeProperties)
        {
            var child = new RgfTreeNodeParameters()
            {
                RowData = node.RowData,
                Property = prop,
                AbsoluteRowIndex = node.AbsoluteRowIndex
            };
            child.EmbeddedGrid = GetEmbeddedGrid(child);
            node.Children.Add(child);
        }

        _nodeCache[absoluteRowIndex] = node;
        return node;
    }

    public RenderFragment? GetEmbeddedGrid(RgfTreeNodeParameters node)
    {
        if (node.EmbeddedGrid == null && node.Property != null)
        {
            var entityParameters = new RgfEntityParameters(node.Property.BaseEntityNameVersion, Manager.SessionParams)
            {
                ParentManager = Manager,
                GridId = null,
                DisplayMode = RfgDisplayMode.Tree
            };

            if (Manager.ListHandler.GetEntityKey(node.RowData, out var key) && key?.IsEmpty != true)
            {
                entityParameters.FilterParent = new RgfFilterParent
                {
                    EntityNameVersion = Manager.EntityDesc.NameVersion,
                    EntityKey = key,
                    PropertyId = node.Property.Id
                };
            }
            node.EntityParameters = entityParameters;
            node.EmbeddedGrid = RgfEntityComponent.Create(entityParameters);
        }
        return node.EmbeddedGrid;
    }

    public override void Dispose()
    {
        base.Dispose();

        EntityParameters.ToolbarParameters.EventDispatcher.Unsubscribe(RgfToolbarEventKind.Refresh, OnRefresh);
    }
}