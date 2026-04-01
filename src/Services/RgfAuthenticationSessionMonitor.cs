using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

internal sealed class RgfAuthenticationSessionMonitor(
    IHttpClientFactory httpClientFactory,
    NavigationManager navigationManager,
    RgfAuthenticationEndpointResolver authenticationEndpoints,
    ILogger<RgfAuthenticationSessionMonitor> logger) : IRgfAuthenticationSessionMonitor
{
    private readonly object _sync = new();
    private bool _hasValidSession = true;
    private bool _routeAuthenticationRequired;
    private int _componentAuthenticationRequirementCount;
    private int _loginNavigationStarted;

    public bool HasValidSession
    {
        get
        {
            lock (_sync)
            {
                return _hasValidSession;
            }
        }
    }

    public event Action? SessionStateChanged;

    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        using var httpClient = httpClientFactory.CreateClient(ApiService.RgfApiClientName);
        using var response = await httpClient.GetAsync(authenticationEndpoints.SessionPath, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && IsReauthenticationRequired(response))
        {
            logger.LogInformation("Authentication session probe returned 401, switching the client to anonymous mode.");
            InvalidateSession(redirectIfRequired: false);
        }
    }

    public void SetRouteAuthenticationRequired(bool requiresAuthentication)
    {
        var shouldRedirect = false;
        lock (_sync)
        {
            _routeAuthenticationRequired = requiresAuthentication;
            shouldRedirect = requiresAuthentication && !_hasValidSession;
        }

        if (shouldRedirect)
        {
            NavigateToLoginOnce();
        }
    }

    public IDisposable BeginAuthenticationRequirementScope()
    {
        var shouldRedirect = false;
        lock (_sync)
        {
            _componentAuthenticationRequirementCount++;
            shouldRedirect = !_hasValidSession;
        }

        if (shouldRedirect)
        {
            NavigateToLoginOnce();
        }

        return new AuthenticationRequirementScope(this);
    }

    public ValueTask HandleUnauthorizedAsync(RgfAuthenticationFailureContext context, CancellationToken cancellationToken)
    {
        if (!context.IsReauthenticationRequired)
        {
            return ValueTask.CompletedTask;
        }

        logger.LogInformation("Unauthorized response received for {RequestUri}.", context.RequestUri);
        InvalidateSession(redirectIfRequired: true);
        return ValueTask.CompletedTask;
    }

    private void EndAuthenticationRequirementScope()
    {
        lock (_sync)
        {
            if (_componentAuthenticationRequirementCount > 0)
            {
                _componentAuthenticationRequirementCount--;
            }
        }
    }

    private void InvalidateSession(bool redirectIfRequired)
    {
        var stateChanged = false;
        var shouldRedirect = false;

        lock (_sync)
        {
            if (_hasValidSession)
            {
                _hasValidSession = false;
                stateChanged = true;
            }

            shouldRedirect = redirectIfRequired && (_routeAuthenticationRequired || _componentAuthenticationRequirementCount > 0);
        }

        if (stateChanged)
        {
            SessionStateChanged?.Invoke();
        }

        if (shouldRedirect)
        {
            NavigateToLoginOnce();
        }
    }

    private void NavigateToLoginOnce()
    {
        if (Interlocked.Exchange(ref _loginNavigationStarted, 1) != 0)
        {
            return;
        }

        var currentUri = new Uri(navigationManager.Uri, UriKind.Absolute);
        var returnUrl = $"{currentUri.PathAndQuery}{currentUri.Fragment}";
        navigationManager.NavigateTo($"{authenticationEndpoints.LoginPath}?returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
    }

    private static bool IsReauthenticationRequired(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues(RgfAuthenticationFailureContext.ReauthenticationRequiredHeaderName, out var values)
            && values.Contains(RgfAuthenticationFailureContext.ReauthenticationRequiredHeaderValue, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class AuthenticationRequirementScope(RgfAuthenticationSessionMonitor owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.EndAuthenticationRequirementScope();
            }
        }
    }
}
