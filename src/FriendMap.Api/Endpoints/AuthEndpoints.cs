using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FriendMap.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/dev-login", async (
            DevLoginRequest request,
            AppDbContext db,
            JwtTokenService jwt,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Nickname))
            {
                return Results.BadRequest("nickname is required.");
            }

            var nickname = NormalizeNickname(request.Nickname);
            var user = await db.Users.FirstOrDefaultAsync(x => x.Nickname == nickname, ct);

            if (user is null)
            {
                user = new AppUser
                {
                    Nickname = nickname,
                    DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? nickname : request.DisplayName.Trim(),
                    AvatarUrl = BuildDevAvatarUrl(nickname)
                };
                db.Users.Add(user);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                var shouldSave = false;

                if (!string.IsNullOrWhiteSpace(request.DisplayName) && user.DisplayName != request.DisplayName.Trim())
                {
                    user.DisplayName = request.DisplayName.Trim();
                    shouldSave = true;
                }

                if (string.IsNullOrWhiteSpace(user.AvatarUrl))
                {
                    user.AvatarUrl = BuildDevAvatarUrl(nickname);
                    shouldSave = true;
                }

                if (shouldSave)
                {
                    user.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }

            return Results.Ok(jwt.CreateToken(user));
        }).AllowAnonymous();

        group.MapGet("/me", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = CurrentUser.GetUserId(user);
            if (userId == Guid.Empty) return Results.Unauthorized();

            var appUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
            return appUser is null
                ? Results.NotFound()
                : Results.Ok(new AuthUserDto(appUser.Id, appUser.Nickname, appUser.DisplayName));
        }).RequireAuthorization();

        return group;
    }

    private static string NormalizeNickname(string nickname)
    {
        return nickname.Trim().ToLowerInvariant().Replace(" ", "-");
    }

    private static string BuildDevAvatarUrl(string nickname)
    {
        return $"https://i.pravatar.cc/160?u={Uri.EscapeDataString(nickname)}";
    }
}
