using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Handlers;
using System;
using System.Linq;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfPagerComponent : ComponentBase, IDisposable
{
    public List<IDisposable> Disposables { get; private set; } = new();

    public int CurrentPage => Manager.ActivePage.Value;

    public int ItemCount { get => Manager.ItemCount.Value; }

    public int PageSize { get => Manager.PageSize.Value; set => Manager.PageSize.Value = value; }

    public int TotalPages { get; private set; }

    public IRgManager Manager { get => EntityParameters.Manager!; }

    public RgfPagerParameters PagerParameters { get => EntityParameters.PagerParameters; }


    protected override void OnInitialized()
    {
        base.OnInitialized();

        Disposables.Add(Manager.ActivePage.OnAfterChange(this, (args) => StateHasChanged()));
        Disposables.Add(Manager.ItemCount.OnAfterChange(this, OnChangeItemCount));
        OnChangeItemCount(new ObservablePropertyEventArgs<int>(0, ItemCount));
    }

    public void OnChangeItemCount(ObservablePropertyEventArgs<int> args)
    {
        TotalPages = PageSize > 0 ? (args.NewData + PageSize - 1) / PageSize : 0;
    }

    public void PageChanging(int page)
    {
        if (page > 0 && page <= TotalPages && CurrentPage != page)
        {
            Manager.ActivePage.Value = page;
        }
    }

    public void PageSizeChanging(string value)
    {
        if (Int32.TryParse(value, out int pageSize))
        {
            PageSizeChanging(pageSize);
        }
    }

    public void PageSizeChanging(int pageSize)
    {
        PageSize = pageSize;
        OnChangeItemCount(new ObservablePropertyEventArgs<int>(0, ItemCount));
        _ = Manager.ListHandler.RefreshDataAsync();
    }

    public void Dispose()
    {
        if (Disposables != null)
        {
            Disposables.ForEach(disposable => disposable.Dispose());
            Disposables = null!;
        }
    }
}
