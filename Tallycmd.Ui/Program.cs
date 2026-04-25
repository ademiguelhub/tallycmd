var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddHttpClient<TallycmdApiClient>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]!))
    .AddHttpMessageHandler<AuthDelegatingHandler>();

builder.Services
    .AddCascadingAuthenticationState()
    .AddScoped<InMemoryTokenStore>()
    .AddScoped<AuthDelegatingHandler>()
    .AddScoped<JwtAuthenticationStateProvider>()
    .AddScoped<AuthenticationStateProvider>(
        sp => sp.GetRequiredService<JwtAuthenticationStateProvider>())
    .AddScoped<IAuthService, AuthService>();

var application = builder.Build();

if (application.Environment.IsDevelopment())
{
    application.UseMigrationsEndPoint();
    application.UseStatusCodePages();
}
else
{
    application
        .UseHsts()
        .UseExceptionHandler("/error", createScopeForErrors: true)
        .UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
}

application.UseHttpsRedirection();

application.UseAntiforgery();

application.MapStaticAssets();

application
    .MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await application.RunAsync();
