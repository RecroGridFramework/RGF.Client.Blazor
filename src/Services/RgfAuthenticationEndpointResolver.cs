using Microsoft.Extensions.Configuration;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

public sealed class RgfAuthenticationEndpointResolver(IConfiguration configuration)
{
    private const string DefaultEndpointBasePath = "/authentication";

    public string BasePath { get; } = NormalizeBasePath(configuration.GetValue<string>("Authentication:Host:EndpointBasePath"));

    public string LoginPath => $"{BasePath}/login";

    public string LogoutPath => $"{BasePath}/logout";

    public string SessionPath => $"{BasePath}/session";

    private static string NormalizeBasePath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return DefaultEndpointBasePath;
        }

        var normalizedPath = configuredPath.Trim();
        if (!normalizedPath.StartsWith("/", StringComparison.Ordinal) || normalizedPath.StartsWith("//", StringComparison.Ordinal))
        {
            return DefaultEndpointBasePath;
        }

        return normalizedPath.TrimEnd('/');
    }
}
