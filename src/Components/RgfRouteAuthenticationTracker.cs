using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Recrovit.RecroGridFramework.Client.Blazor.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.Components;

public sealed class RgfRouteAuthenticationTracker : ComponentBase
{
    [Inject]
    public IRgfAuthenticationSessionMonitor SessionMonitor { get; set; } = null!;

    [Parameter, EditorRequired]
    public Type PageType { get; set; } = null!;

    protected override void OnParametersSet()
    {
        SessionMonitor.SetRouteAuthenticationRequired(RequiresAuthentication(PageType));
    }

    private static bool RequiresAuthentication(Type pageType)
    {
        if (pageType.IsDefined(typeof(AllowAnonymousAttribute), inherit: true))
        {
            return false;
        }

        return pageType.IsDefined(typeof(AuthorizeAttribute), inherit: true);
    }
}
