using Recrovit.RecroGridFramework.Client.Services;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

public interface IRgfAuthenticationSessionMonitor : IRgfAuthenticationFailureHandler
{
    bool HasValidSession { get; }

    event Action? SessionStateChanged;

    Task ProbeAsync(CancellationToken cancellationToken);

    void SetRouteAuthenticationRequired(bool requiresAuthentication);

    IDisposable BeginAuthenticationRequirementScope();
}
