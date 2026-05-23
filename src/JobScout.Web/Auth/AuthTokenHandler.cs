using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace JobScout.Web.Auth;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly IJSRuntime _js;

    public AuthTokenHandler(IJSRuntime js)
    {
        _js = js;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", "auth_token");
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch
        {
            // During prerendering, JS interop is not available — skip silently
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
