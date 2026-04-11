namespace Tallycmd.Ui.Auth;

public class JwtAuthenticationStateProvider(
    InMemoryTokenStore tokenStore,
    TallycmdApiClient client) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (tokenStore.AccessToken is { } existing)
            return BuildState(existing);

        // Attempt silent refresh — browser sends the HttpOnly cookie automatically
        try
        {
            var response = await client.PostAsync("/api/auth/refresh", null);
            if (!response.IsSuccessStatusCode)
                return Anonymous;

            var result = await response.Content.ReadFromJsonAsync<RefreshResponse>();
            if (result is null)
                return Anonymous;

            tokenStore.SetToken(result.AccessToken);
            return BuildState(result.AccessToken);
        }
        catch
        {
            return Anonymous;
        }
    }

    public void NotifyUserAuthenticated(string token)
    {
        tokenStore.SetToken(token);
        NotifyAuthenticationStateChanged(Task.FromResult(BuildState(token)));
    }

    public void NotifyUserLoggedOut()
    {
        tokenStore.ClearToken();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private static AuthenticationState BuildState(string token)
    {
        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string token)
    {
        var payload = token.Split('.')[1];
        var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var bytes = Convert.FromBase64String(padded);
        var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bytes);
        return json?.Select(kv => new Claim(kv.Key, kv.Value.ToString() ?? ""))
               ?? [];
    }
}
