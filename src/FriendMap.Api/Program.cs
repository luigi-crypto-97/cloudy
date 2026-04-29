using FriendMap.Api.Data;
using FriendMap.Api.Endpoints;
using FriendMap.Api.Hubs;
using FriendMap.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseWebRoot("wwwroot");

builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Inserisci un JWT ottenuto da /api/auth/dev-login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<ApnsOptions>(builder.Configuration.GetSection("Apns"));
builder.Services.Configure<NotificationDispatchOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.Configure<UniversalLinksOptions>(builder.Configuration.GetSection("UniversalLinks"));
builder.Services.Configure<FoursquareOptions>(builder.Configuration.GetSection("Foursquare"));
builder.Services.Configure<OverpassOptions>(builder.Configuration.GetSection("Overpass"));
builder.Services.Configure<MediaStorageOptions>(builder.Configuration.GetSection("MediaStorage"));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddMemoryCache();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        npgsql => npgsql.UseNetTopologySuite())
    .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<AffluenceAggregationService>();
builder.Services.AddScoped<ModerationService>();
builder.Services.AddScoped<VenueAnalyticsService>();
builder.Services.AddScoped<MediaStorageService>();
var foursquareOptions = builder.Configuration.GetSection("Foursquare").Get<FoursquareOptions>() ?? new FoursquareOptions();
var overpassOptions = builder.Configuration.GetSection("Overpass").Get<OverpassOptions>() ?? new OverpassOptions();
builder.Services.AddHttpClient<FoursquareVenueImportService>(client =>
{
    client.BaseAddress = new Uri(foursquareOptions.BaseUrl);
    client.DefaultRequestVersion = System.Net.HttpVersion.Version11;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FriendMap/1.0");
});
builder.Services.AddHttpClient<OverpassVenueImportService>(client =>
{
    client.BaseAddress = new Uri(overpassOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(overpassOptions.TimeoutSeconds + 10, 15, 240));
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FriendMap/1.0 (local development venue import)");
});
builder.Services.AddHttpClient("nominatim", client =>
{
    client.BaseAddress = new Uri(overpassOptions.NominatimBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FriendMap/1.0 (local development venue import; contact: api.iron-quote.it)");
});
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<NotificationOutboxService>();
builder.Services.AddSingleton<ApnsClient>();
builder.Services.AddHostedService<NotificationDispatchService>();
builder.Services.Configure<PrivacyOptions>(builder.Configuration.GetSection("Privacy"));

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSignalR();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global rate limit per IP (anonymous) or per user ID (authenticated).
    // The mobile app refreshes map overlays, stories and chat in parallel; keep this
    // protective without blocking normal in-app bursts.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (context.Request.Path.StartsWithSegments("/uploads") ||
            context.Request.Path.StartsWithSegments("/.well-known") ||
            context.Request.Path.StartsWithSegments("/health"))
        {
            return RateLimitPartition.GetNoLimiter("static-or-health");
        }

        var key = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anon"
            : context.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 600,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 20
            });
    });

    // Social endpoints include reads and writes for map overlays, stories, chat and
    // flares. A small queue avoids false 429s when the app fires concurrent actions.
    options.AddPolicy("social", context =>
    {
        var userId = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anon"
            : "anon";
        var isRead = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{userId}:{(isRead ? "read" : "write")}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = isRead ? 480 : 120,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = isRead ? 20 : 8
            });
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("default", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowAnyOrigin();
    });
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
        if (feature?.Error is { } exception)
        {
            Log.Error(exception, "Unhandled API exception on {Path}", feature.Path);
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
            title: "Errore server",
            detail: "Si e verificato un errore interno. Controlla i log del server per il dettaglio tecnico.",
            statusCode: StatusCodes.Status500InternalServerError)
            .ExecuteAsync(context);
    });
});

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseCors("default");
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ChatHub>("/hubs/chat");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    if (app.Environment.IsDevelopment() &&
        app.Configuration.GetValue("Database:SeedDevelopmentData", true))
    {
        await DevelopmentDataSeeder.SeedAsync(db);
    }
}

app.MapGet("/", () => Results.Ok(new
{
    service = "FriendMap.Api",
    status = "ok",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/health/db", async (AppDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect
        ? Results.Ok(new { status = "healthy", database = "connected", utc = DateTimeOffset.UtcNow })
        : Results.Problem("Database connection failed", statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/.well-known/apple-app-site-association", (Microsoft.Extensions.Options.IOptions<UniversalLinksOptions> options) =>
{
    var appIds = options.Value.IosAppIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    return Results.Json(new
    {
        applinks = new
        {
            apps = Array.Empty<string>(),
            details = appIds.Select(appId => new
            {
                appID = appId,
                paths = new[] { "/l/*" }
            }).ToArray()
        },
        webcredentials = new
        {
            apps = appIds
        }
    });
});

app.MapGet("/l/{type}/{id}", (string type, string id) =>
{
    var normalizedType = type.Trim().ToLowerInvariant();
    var title = normalizedType switch
    {
        "table" => "Apri tavolo Cloudy",
        "chat" => "Apri chat Cloudy",
        "venue" => "Apri locale Cloudy",
        _ => "Apri Cloudy"
    };

    var html = $$"""
                 <!doctype html>
                 <html lang="it">
                 <head>
                   <meta charset="utf-8">
                   <meta name="viewport" content="width=device-width, initial-scale=1">
                   <title>{{title}}</title>
                   <style>
                     body { font-family: -apple-system, BlinkMacSystemFont, sans-serif; background:#0b1020; color:#f8fafc; margin:0; display:grid; place-items:center; min-height:100vh; }
                     main { width:min(92vw,480px); padding:32px 24px; border-radius:24px; background:rgba(15,23,42,.88); box-shadow:0 24px 80px rgba(0,0,0,.35); }
                     h1 { margin:0 0 12px; font-size:28px; }
                     p { margin:0 0 18px; line-height:1.5; color:#cbd5e1; }
                     code { color:#a78bfa; }
                   </style>
                 </head>
                 <body>
                   <main>
                     <h1>{{title}}</h1>
                     <p>Se Cloudy è installata, iOS aprirà direttamente l'app. Altrimenti puoi copiare questo link o aprirlo dopo l'installazione.</p>
                     <p><code>/l/{{normalizedType}}/{{id}}</code></p>
                   </main>
                 </body>
                 </html>
                 """;

    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapVenueEndpoints();
app.MapSocialEndpoints();
app.MapMessagingEndpoints();
app.MapSafetyEndpoints();
app.MapAdminEndpoints();
app.MapNotificationEndpoints();
app.MapStoriesEndpoints();
app.MapDiscoveryEndpoints();
app.MapGamificationEndpoints();

app.Run();

public partial class Program;
