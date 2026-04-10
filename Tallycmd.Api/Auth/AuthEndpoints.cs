namespace Tallycmd.Api.Auth;

public static class AuthEndpoints
{
    private const string RefreshTokenCookie = "refreshToken";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", Login);
        group.MapPost("/refresh", Refresh);
        group.MapPost("/logout", Logout);

        return app;
    }

    private static async Task<IResult> Login(
        [FromBody] LoginRequest request,
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        HttpContext ctx)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Results.Unauthorized();

        var accessToken = GenerateJwt(user, config);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(
            config.GetValue<int>("Jwt:RefreshTokenExpiryDays"));
        await userManager.UpdateAsync(user);

        SetRefreshCookie(ctx, refreshToken, config);

        return Results.Ok(new LoginResponse(accessToken, user.Email!, user.Id));
    }

    private static async Task<IResult> Refresh(
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        HttpContext ctx)
    {
        if (!ctx.Request.Cookies.TryGetValue(RefreshTokenCookie, out var incomingToken))
            return Results.Unauthorized();

        var user = await userManager.Users.SingleOrDefaultAsync(u =>
            u.RefreshToken == incomingToken &&
            u.RefreshTokenExpiry > DateTime.UtcNow);

        if (user is null)
            return Results.Unauthorized();

        var accessToken = GenerateJwt(user, config);
        var newRefreshToken = GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(
            config.GetValue<int>("Jwt:RefreshTokenExpiryDays"));
        await userManager.UpdateAsync(user);

        SetRefreshCookie(ctx, newRefreshToken, config);

        return Results.Ok(new RefreshResponse(accessToken));
    }

    private static async Task<IResult> Logout(
        UserManager<ApplicationUser> userManager,
        HttpContext ctx)
    {
        if (ctx.Request.Cookies.TryGetValue(RefreshTokenCookie, out var token))
        {
            var user = await userManager.Users.SingleOrDefaultAsync(u => u.RefreshToken == token);
            if (user is not null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiry = null;
                await userManager.UpdateAsync(user);
            }
        }

        ctx.Response.Cookies.Delete(RefreshTokenCookie);
        return Results.NoContent();
    }

    private static string GenerateJwt(ApplicationUser user, IConfiguration config)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(config.GetValue<int>("Jwt:AccessTokenExpiryMinutes")),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static void SetRefreshCookie(HttpContext ctx, string token, IConfiguration config)
    {
        ctx.Response.Cookies.Append(RefreshTokenCookie, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(
                config.GetValue<int>("Jwt:RefreshTokenExpiryDays")),
        });
    }
}
