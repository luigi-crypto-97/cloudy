using FriendMap.Api.Data;
using FriendMap.Api.Endpoints;
using FriendMap.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console();
});

builder.Services.AddEndpointsApiExplorer();
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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        npgsql => npgsql.UseNetTopologySuite())
    .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<AffluenceAggregationService>();
builder.Services.AddScoped<ModerationService>();
builder.Services.AddScoped<VenueAnalyticsService>();
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

app.UseSerilogRequestLogging();
app.UseCors("default");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (app.Configuration.GetValue("Database:SeedDevelopmentData", true))
        {
            await DevelopmentDataSeeder.SeedAsync(db);
        }
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

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapVenueEndpoints();
app.MapSocialEndpoints();
app.MapMessagingEndpoints();
app.MapSafetyEndpoints();
app.MapAdminEndpoints();
app.MapNotificationEndpoints();

app.Run();
