namespace Tallycmd.Ui.Services;

public class AuthService(
    IHttpClientFactory httpClientFactory,
    JwtAuthenticationStateProvider authStateProvider) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var client = httpClientFactory.CreateClient("TallycmdApi");
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (result is null)
            return null;

        authStateProvider.NotifyUserAuthenticated(result.AccessToken);
        return result;
    }

    public async Task LogoutAsync()
    {
        var client = httpClientFactory.CreateClient("TallycmdApi");
        await client.PostAsync("/api/auth/logout", null);
        authStateProvider.NotifyUserLoggedOut();
    }
}
