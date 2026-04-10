namespace Tallycmd.Ui.Auth;

public class InMemoryTokenStore
{
    public string? AccessToken { get; private set; }

    public void SetToken(string token) => AccessToken = token;

    public void ClearToken() => AccessToken = null;
}
