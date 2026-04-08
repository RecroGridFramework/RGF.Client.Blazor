using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

internal sealed class RgfAuthenticationPrincipalSnapshotSynchronizer(
    IHttpClientFactory httpClientFactory,
    RgfAuthenticationEndpointResolver authenticationEndpoints,
    IRgfAuthenticationSessionMonitor sessionMonitor,
    RgfAuthenticationPrincipalFactory principalFactory,
    ILogger<RgfAuthenticationPrincipalSnapshotSynchronizer> logger)
{
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private AuthenticationState? _cachedState;
    private int _initialized;

    public async Task<AuthenticationState> SynchronizeAsync(AuthenticationState state, CancellationToken cancellationToken = default)
    {
        if (state.User.Identity?.IsAuthenticated is not true)
        {
            Clear();
            return state;
        }

        var cachedState = _cachedState;
        if (cachedState != null)
        {
            return cachedState;
        }

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            cachedState = _cachedState;
            if (cachedState != null)
            {
                return cachedState;
            }

            using var httpClient = httpClientFactory.CreateClient(ApiService.RgfApiClientName);
            using var response = await httpClient.GetAsync(authenticationEndpoints.PrincipalPath, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var snapshot = await response.Content.ReadFromJsonAsync<RgfAuthenticationPrincipalSnapshot>(cancellationToken: cancellationToken);
                if (snapshot?.IsAuthenticated == true)
                {
                    cachedState = new AuthenticationState(principalFactory.Create(snapshot));
                    _cachedState = cachedState;
                    return cachedState;
                }

                logger.LogDebug("Authentication principal snapshot endpoint returned no authenticated snapshot.");
                return state;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && IsReauthenticationRequired(response))
            {
                logger.LogInformation("Authentication principal snapshot request returned 401, switching the client to anonymous mode.");
                await sessionMonitor.HandleUnauthorizedAsync(new RgfAuthenticationFailureContext
                {
                    StatusCode = response.StatusCode,
                    RequestUri = authenticationEndpoints.PrincipalPath,
                    IsReauthenticationRequired = true,
                    ResponseHeaders = CreateResponseHeaders(response)
                }, cancellationToken);

                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            logger.LogWarning("Authentication principal snapshot request failed with {StatusCode}. Falling back to the underlying provider state.", response.StatusCode);
            return state;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to synchronize the authentication principal snapshot. Falling back to the underlying provider state.");
            return state;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public void Clear() => _cachedState = null;

    public void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 0)
        {
            sessionMonitor.SessionStateChanged += OnSessionStateChanged;
        }
    }

    private void OnSessionStateChanged() => Clear();

    private static bool IsReauthenticationRequired(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues(RgfAuthenticationFailureContext.ReauthenticationRequiredHeaderName, out var values)
            && values.Contains(RgfAuthenticationFailureContext.ReauthenticationRequiredHeaderValue, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string[]> CreateResponseHeaders(HttpResponseMessage response)
    {
        return response.Headers
            .Concat(response.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(header => header.Value).ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }
}
