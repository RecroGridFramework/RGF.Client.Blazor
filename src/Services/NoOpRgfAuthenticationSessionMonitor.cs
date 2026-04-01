using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

internal sealed class NoOpRgfAuthenticationSessionMonitor : IRgfAuthenticationSessionMonitor
{
    private sealed class NoOpScope : IDisposable
    {
        public static readonly NoOpScope Instance = new();

        public void Dispose() { }
    }

    public bool HasValidSession => true;

    public event Action? SessionStateChanged
    {
        add { }
        remove { }
    }

    public Task ProbeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void SetRouteAuthenticationRequired(bool requiresAuthentication) { }

    public IDisposable BeginAuthenticationRequirementScope() => NoOpScope.Instance;

    public ValueTask HandleUnauthorizedAsync(RgfAuthenticationFailureContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
