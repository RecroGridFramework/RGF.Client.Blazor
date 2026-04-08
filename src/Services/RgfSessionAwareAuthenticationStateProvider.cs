using Microsoft.AspNetCore.Components.Authorization;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

internal sealed class RgfSessionAwareAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState AnonymousState = new(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()));

    private readonly AuthenticationStateProvider _innerProvider;
    private readonly IRgfAuthenticationSessionMonitor _sessionMonitor;
    private readonly RgfAuthenticationPrincipalSnapshotSynchronizer _principalSnapshotSynchronizer;

    public RgfSessionAwareAuthenticationStateProvider(
        AuthenticationStateProvider innerProvider,
        IRgfAuthenticationSessionMonitor sessionMonitor,
        RgfAuthenticationPrincipalSnapshotSynchronizer principalSnapshotSynchronizer)
    {
        _innerProvider = innerProvider;
        _sessionMonitor = sessionMonitor;
        _principalSnapshotSynchronizer = principalSnapshotSynchronizer;
        _principalSnapshotSynchronizer.Initialize();
        _innerProvider.AuthenticationStateChanged += OnInnerAuthenticationStateChanged;
        _sessionMonitor.SessionStateChanged += OnSessionStateChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var state = await _innerProvider.GetAuthenticationStateAsync();
        if (!_sessionMonitor.HasValidSession)
        {
            return AnonymousState;
        }

        return await _principalSnapshotSynchronizer.SynchronizeAsync(state);
    }

    public void Dispose()
    {
        _innerProvider.AuthenticationStateChanged -= OnInnerAuthenticationStateChanged;
        _sessionMonitor.SessionStateChanged -= OnSessionStateChanged;
    }

    private void OnInnerAuthenticationStateChanged(Task<AuthenticationState> stateTask) => NotifyAuthenticationStateChanged(WrapStateAsync(stateTask));

    private void OnSessionStateChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private async Task<AuthenticationState> WrapStateAsync(Task<AuthenticationState> stateTask)
    {
        var state = await stateTask;
        _principalSnapshotSynchronizer.Clear();

        if (!_sessionMonitor.HasValidSession)
        {
            return AnonymousState;
        }

        return await _principalSnapshotSynchronizer.SynchronizeAsync(state);
    }
}
