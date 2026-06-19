namespace Tallycmd.Ui.Auth;

public class AuthDelegatingHandler(InMemoryTokenStore tokenStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (tokenStore.AccessToken is { } token)
            request.Headers.Authorization = new("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
