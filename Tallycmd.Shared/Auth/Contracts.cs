namespace Tallycmd.Shared.Auth;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string AccessToken, string Email, string UserId);
public record RefreshResponse(string AccessToken);
