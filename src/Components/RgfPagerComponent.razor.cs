using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public partial class RgfPagerComponent : ComponentBase, IDisposable
{
    public List<IDisposable> Disposables { get; private set; } = [];

    public int CurrentPage => Manager.ActivePage.Value;

    public int ItemCount => Manager.ItemCount.Value;

    public int PageSize { get => Manager.PageSize.Value; set => Manager.PageSize.Value = value; }

    public int TotalPages { get; private set; }

    public IRgManager Manager => EntityParameters.Manager!;

    public RgfPagerParameters PagerParameters => EntityParameters.PagerParameters;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Disposables.Add(Manager.ActivePage.OnAfterChange(this, (args) => StateHasChanged()));
        Disposables.Add(Manager.ItemCount.OnAfterChange(this, OnChangeItemCount));
        Disposables.Add(Manager.PageSize.OnAfterChange(this, (args) => { StateHasChanged(); }));
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

    public Task PageSizeChanging(string value)
    {
        if (Int32.TryParse(value, out int pageSize) && pageSize > 0)
        {
            return PageSizeChanging(pageSize);
        }
        return Task.CompletedTask;
    }

    public Task PageSizeChanging(int pageSize)
    {
        PageSize = pageSize;
        OnChangeItemCount(new ObservablePropertyEventArgs<int>(0, ItemCount));
        return Manager.ListHandler.RefreshDataAsync();
    }

    public void OnKeyDown(KeyboardEventArgs args)
    {
        switch (args.Code)
        {
            case "Home":
                PageChanging(1);
                break;

            case "PageUp":
                PageChanging(CurrentPage - 1);
                break;

            case "PageDown":
                PageChanging(CurrentPage + 1);
                break;

            case "End":
                PageChanging(TotalPages);
                break;
        }
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
