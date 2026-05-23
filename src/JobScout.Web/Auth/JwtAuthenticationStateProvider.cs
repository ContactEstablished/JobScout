using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace JobScout.Web.Auth;

public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public JwtAuthenticationStateProvider(IJSRuntime js)
    {
        _js = js;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _js.InvokeAsync<string?>("localStorage.getItem", "auth_token");

        if (string.IsNullOrWhiteSpace(token))
            return Anonymous;

        var claims = ParseToken(token);
        if (claims is null)
            return Anonymous;

        return new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt")));
    }

    public async Task MarkUserAsAuthenticated(string token)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", "auth_token", token);

        var claims = ParseToken(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public async Task MarkUserAsLoggedOut()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", "auth_token");
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private static IEnumerable<Claim>? ParseToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            if (jwt.ValidTo < DateTime.UtcNow)
                return null;

            return jwt.Claims;
        }
        catch
        {
            return null;
        }
    }
}
