using System.Net.Http.Headers;

namespace NebulaPanel.Web.Services;

public class AuthenticatedHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthStateProvider _authStateProvider;

    public AuthenticatedHttpClient(HttpClient httpClient, AuthStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _authStateProvider = authStateProvider;
    }

    public async Task<HttpClient> GetClientAsync()
    {
        var token = await _authStateProvider.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
        return _httpClient;
    }
}
