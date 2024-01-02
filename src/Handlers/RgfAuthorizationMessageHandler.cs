using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Recrovit.RecroGridFramework.Client.Services;
using System;
using System.Linq;
using System.Text;

namespace Recrovit.RecroGridFramework.Client.Blazor.Handlers;

public class RgfAuthorizationMessageHandler : AuthorizationMessageHandler
{
    private static string? _authorizedUrls;
    private static string[]? _scopes;

    public RgfAuthorizationMessageHandler(IConfiguration configuration, IAccessTokenProvider provider, NavigationManager navigation) : base(provider, navigation)
    {
        if (_authorizedUrls == null)
        {
            _authorizedUrls = ApiService.BaseAddress;
            var config = configuration.GetSection("Recrovit:RecroGridFramework");
            _scopes = config.GetSection("API:DefaultScopes").Get<string[]>() ?? new[] { "openid", "profile" };
        }
        ConfigureHandler(new string[] { _authorizedUrls }, _scopes);
    }
}