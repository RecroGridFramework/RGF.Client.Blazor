using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Client.Blazor.Handlers;

internal sealed class NoOpRgfServerRequestCookieAccessor : IRgfServerRequestCookieAccessor
{
    public string? GetCookieHeader() => null;
}
