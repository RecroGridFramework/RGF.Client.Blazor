using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Client.Blazor.Events;

public class GridEvents
{
    private Dictionary<string, object> _dispatchers = new();

    public EventDispatcher<DataEventArgs<RgfDynamicDictionary>> CreateAttributes 
        => ((EventDispatcher<DataEventArgs<RgfDynamicDictionary>>)_dispatchers.GetOrCreate(nameof(CreateAttributes), () => new EventDispatcher<DataEventArgs<RgfDynamicDictionary>>()));

    public EventDispatcher<DataEventArgs<IEnumerable<RgfProperty>>> ColumnSettingsChanged
        => ((EventDispatcher<DataEventArgs<IEnumerable<RgfProperty>>>)_dispatchers.GetOrCreate(nameof(ColumnSettingsChanged), () => new EventDispatcher<DataEventArgs<IEnumerable<RgfProperty>>>()));
}
