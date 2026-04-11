namespace Tallycmd.Ui.Services;

public class AuthService(
    TallycmdApiClient client,
    JwtAuthenticationStateProvider authStateProvider) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
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
        await client.PostAsync("/api/auth/logout", null);
        authStateProvider.NotifyUserLoggedOut();
    }
}
