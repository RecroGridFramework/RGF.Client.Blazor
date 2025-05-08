using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Recrovit.RecroGridFramework.Client.Services;
using System.Net;

namespace Recrovit.RecroGridFramework.Client.Blazor.Handlers;

public class RgfAuthorizationMessageHandler : AuthorizationMessageHandler
{
    public static string LoginPath { get; set; } = RemoteAuthenticationDefaults.LoginPath;

    private static string? _authorizedUrls;
    private static string[]? _scopes;
    private readonly ILogger<RgfAuthorizationMessageHandler> _logger;
    private readonly NavigationManager _navigation;

    public RgfAuthorizationMessageHandler(ILogger<RgfAuthorizationMessageHandler> logger, IConfiguration configuration, IAccessTokenProvider provider, NavigationManager navigation) : base(provider, navigation)
    {
        _logger = logger;
        _navigation = navigation;
        if (_authorizedUrls == null)
        {
            _authorizedUrls = ApiService.BaseAddress;
            var config = configuration.GetSection("Recrovit:RecroGridFramework");
            _scopes = config.GetSection("API:DefaultScopes").Get<string[]>() ?? ["openid", "profile"];
        }
        ConfigureHandler([_authorizedUrls], _scopes);
    }

    private HttpResponseMessage RedirectOnAccessTokenFailure(AccessTokenNotAvailableException ex)
    {
        _logger.LogDebug("AccessTokenNotAvailableException => Redirect");
        ex.Redirect((e) =>
        {
            _navigation.NavigateToLogin(LoginPath, new InteractiveRequestOptions()
            {
                Interaction = InteractionType.SignIn,
                ReturnUrl = _navigation.Uri
            });
        });
        return new HttpResponseMessage(HttpStatusCode.Unauthorized);
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return base.Send(request, cancellationToken);
        }
        catch (AccessTokenNotAvailableException ex)
        {
            return RedirectOnAccessTokenFailure(ex);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (AccessTokenNotAvailableException ex)
        {
            return RedirectOnAccessTokenFailure(ex);
        }
    }
}