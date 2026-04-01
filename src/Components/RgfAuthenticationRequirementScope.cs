using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Recrovit.RecroGridFramework.Client.Blazor.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public sealed class RgfAuthenticationRequirementScope : ComponentBase, IDisposable
{
    private IDisposable? _scope;

    [Inject]
    public IRgfAuthenticationSessionMonitor SessionMonitor { get; set; } = null!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void OnInitialized()
    {
        _scope = SessionMonitor.BeginAuthenticationRequirementScope();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddContent(0, ChildContent);
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }
}
