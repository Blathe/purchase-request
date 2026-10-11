using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using PurchaseRequest.Components;
using PurchaseRequest.Data;
using PurchaseRequest.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();

// Sign in with Microsoft Entra ID (FR-01). Token acquisition uses the authorization
// code flow, which needs AzureAd:ClientSecret, and is used later for Microsoft Graph.
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

// Create or refresh the user's AppUser row each time they sign in.
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    var onTokenValidated = options.Events.OnTokenValidated;
    options.Events.OnTokenValidated = async context =>
    {
        await onTokenValidated(context);
        var userSync = context.HttpContext.RequestServices.GetRequiredService<UserSyncService>();
        await userSync.SyncAsync(context.Principal!, context.HttpContext.RequestAborted);
    };
});

// Every page requires a signed-in user unless marked [AllowAnonymous] (NFR-01).
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddCascadingAuthenticationState();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
// Blazor Server components create a short-lived DbContext per operation from the factory.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<UserSyncService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<RequestService>();
builder.Services.AddSingleton<IRequestNumberGenerator, SqlRequestNumberGenerator>();
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddScoped<PurchaseRequest.Services.NotificationService>();
builder.Services.AddScoped<PurchasingService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<IEmailSender, LogEmailSender>();
builder.Services.AddHttpClient<IDirectoryService, GraphDirectoryService>(client =>
    client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var authentication = app.MapGroup("/authentication");

authentication.MapGet("/login", (string? returnUrl) =>
    TypedResults.Challenge(new AuthenticationProperties { RedirectUri = LocalUrlOrRoot(returnUrl) }))
    .AllowAnonymous();

authentication.MapPost("/logout", ([FromForm] string? returnUrl) =>
    TypedResults.SignOut(new AuthenticationProperties { RedirectUri = LocalUrlOrRoot(returnUrl) },
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));

app.Run();

// Only allow redirects back into this app, so login/logout cannot be used as an open redirect.
static string LocalUrlOrRoot(string? returnUrl)
{
    if (string.IsNullOrEmpty(returnUrl))
    {
        return "/";
    }

    var path = returnUrl.StartsWith('/') ? returnUrl : "/" + returnUrl;
    return path.StartsWith("//") || path.StartsWith("/\\") ? "/" : path;
}
