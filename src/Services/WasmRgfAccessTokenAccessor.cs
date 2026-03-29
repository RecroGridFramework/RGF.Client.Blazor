using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;

namespace Recrovit.RecroGridFramework.Client.Blazor.Services;

public sealed class WasmRgfAccessTokenAccessor : IRgfAccessTokenAccessor
{
    private readonly IAccessTokenProvider _accessTokenProvider;

    public WasmRgfAccessTokenAccessor(IAccessTokenProvider accessTokenProvider)
    {
        _accessTokenProvider = accessTokenProvider;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        var tokenResult = await _accessTokenProvider.RequestAccessToken();
        return tokenResult.TryGetToken(out var token) ? token.Value : null;
    }
}
