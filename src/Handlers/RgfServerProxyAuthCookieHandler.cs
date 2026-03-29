using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.Handlers;

/// <summary>
/// Forwards the incoming authentication cookie when SSR code calls the local host proxy.
/// </summary>
public sealed class RgfServerProxyAuthCookieHandler(IRgfServerRequestCookieAccessor cookieAccessor) : DelegatingHandler
{
    private const string CookieHeaderName = "Cookie";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (ShouldForwardCookie(request, out var cookieHeaderValue))
        {
            request.Headers.TryAddWithoutValidation(CookieHeaderName, cookieHeaderValue);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private bool ShouldForwardCookie(HttpRequestMessage request, out string cookieHeaderValue)
    {
        cookieHeaderValue = string.Empty;

        if (request.Headers.Contains(CookieHeaderName))
        {
            return false;
        }

        if (!IsLocalProxyRequest(request.RequestUri))
        {
            return false;
        }

        var cookie = cookieAccessor.GetCookieHeader();
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return false;
        }

        cookieHeaderValue = cookie;
        return true;
    }

    private static bool IsLocalProxyRequest(Uri? requestUri)
    {
        if (requestUri == null)
        {
            return false;
        }

        if (!requestUri.IsAbsoluteUri)
        {
            return true;
        }

        if (!Uri.TryCreate(ApiService.BaseAddress, UriKind.Absolute, out var proxyBaseAddress))
        {
            return false;
        }

        return Uri.Compare(requestUri, proxyBaseAddress, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }
}
