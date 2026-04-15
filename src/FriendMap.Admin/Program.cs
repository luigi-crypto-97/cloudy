using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "FriendMap.Admin";
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddHttpClient("api", client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:8080/";
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/login", () => Results.Content(RenderLoginPage(), "text/html")).AllowAnonymous();

app.MapPost("/login", async (HttpContext context, IConfiguration configuration) =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    var expectedUsername = configuration["Admin:Username"] ?? "admin";
    var expectedPassword = configuration["Admin:Password"] ?? "admin_dev";

    if (!string.Equals(username, expectedUsername, StringComparison.Ordinal) ||
        !string.Equals(password, expectedPassword, StringComparison.Ordinal))
    {
        return Results.Content(RenderLoginPage("Credenziali non valide."), "text/html", statusCode: StatusCodes.Status401Unauthorized);
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, username),
        new(ClaimTypes.Role, "Admin")
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Redirect("/");
}).AllowAnonymous().DisableAntiforgery();

app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapBlazorHub().RequireAuthorization();
app.MapFallbackToPage("/_Host").RequireAuthorization();

app.Run();

static string RenderLoginPage(string? error = null)
{
    var errorMarkup = string.IsNullOrWhiteSpace(error)
        ? string.Empty
        : $"""<p class="error">{System.Net.WebUtility.HtmlEncode(error)}</p>""";

    return $$"""
        <!DOCTYPE html>
        <html lang="it">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>FriendMap Admin Login</title>
            <style>
                body { margin: 0; min-height: 100vh; display: grid; place-items: center; font-family: Arial, Helvetica, sans-serif; background: #f4f7fb; color: #1f2937; }
                main { width: min(360px, calc(100vw - 32px)); background: white; border: 1px solid #e5e7eb; border-radius: 8px; padding: 24px; box-shadow: 0 2px 10px rgba(0,0,0,.06); }
                h1 { margin: 0 0 16px; font-size: 24px; }
                label { display: block; font-size: 13px; color: #475569; margin: 14px 0 6px; }
                input { width: 100%; box-sizing: border-box; padding: 10px 12px; border: 1px solid #cbd5e1; border-radius: 6px; font-size: 15px; }
                button { width: 100%; margin-top: 18px; background: #2563eb; color: white; border: 0; padding: 11px 14px; border-radius: 6px; font-size: 15px; }
                .error { color: #991b1b; background: #fee2e2; border: 1px solid #fecaca; padding: 10px 12px; border-radius: 6px; }
            </style>
        </head>
        <body>
            <main>
                <h1>FriendMap Admin</h1>
                {{errorMarkup}}
                <form method="post" action="/login">
                    <label for="username">Username</label>
                    <input id="username" name="username" autocomplete="username" autofocus />
                    <label for="password">Password</label>
                    <input id="password" name="password" type="password" autocomplete="current-password" />
                    <button type="submit">Accedi</button>
                </form>
            </main>
        </body>
        </html>
        """;
}
