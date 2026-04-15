using FriendMap.Api.Data;
using FriendMap.Api.Endpoints;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        npgsql => npgsql.UseNetTopologySuite()));

builder.Services.AddScoped<AffluenceAggregationService>();
builder.Services.AddScoped<ModerationService>();
builder.Services.AddScoped<VenueAnalyticsService>();
builder.Services.Configure<PrivacyOptions>(builder.Configuration.GetSection("Privacy"));

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new
{
    service = "FriendMap.Api",
    status = "ok",
    utc = DateTimeOffset.UtcNow
}));

app.MapVenueEndpoints();
app.MapSocialEndpoints();
app.MapAdminEndpoints();

app.Run();
