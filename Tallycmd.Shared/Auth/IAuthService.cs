namespace Tallycmd.Shared.Auth;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task LogoutAsync();
}
