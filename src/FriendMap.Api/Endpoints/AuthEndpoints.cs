using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using FriendMap.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

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

        group.MapPost("/apple", async (
            AppleLoginRequest request,
            AppDbContext db,
            AppleAuthService appleAuth,
            JwtTokenService jwt,
            CancellationToken ct) =>
        {
            AppleIdentity identity;
            try
            {
                identity = await appleAuth.ValidateIdentityTokenAsync(request.IdentityToken, ct);
            }
            catch (AppleAuthException)
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FirstOrDefaultAsync(x => x.AppleSubject == identity.Subject, ct);
            if (user is null && !string.IsNullOrWhiteSpace(identity.Email))
            {
                user = await db.Users.FirstOrDefaultAsync(x => x.DiscoverableEmailNormalized == identity.Email, ct);
            }

            if (user is null)
            {
                var nickname = await BuildUniqueAppleNicknameAsync(db, request.FullName, identity, ct);
                user = new AppUser
                {
                    Nickname = nickname,
                    DisplayName = BuildDisplayName(request.FullName, nickname),
                    AppleSubject = identity.Subject,
                    DiscoverableEmailNormalized = identity.Email,
                    AvatarUrl = BuildDevAvatarUrl(nickname)
                };
                db.Users.Add(user);
            }
            else
            {
                user.AppleSubject ??= identity.Subject;
                user.DiscoverableEmailNormalized ??= identity.Email;
                if (!string.IsNullOrWhiteSpace(request.FullName) && string.IsNullOrWhiteSpace(user.DisplayName))
                {
                    user.DisplayName = request.FullName.Trim();
                }
                if (string.IsNullOrWhiteSpace(user.AvatarUrl))
                {
                    user.AvatarUrl = BuildDevAvatarUrl(user.Nickname);
                }
                user.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(ct);
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

    private static async Task<string> BuildUniqueAppleNicknameAsync(
        AppDbContext db,
        string? fullName,
        AppleIdentity identity,
        CancellationToken ct)
    {
        var baseNickname = Slugify(fullName)
            ?? Slugify(identity.Email?.Split('@').FirstOrDefault())
            ?? $"apple-{identity.Subject[..Math.Min(identity.Subject.Length, 8)].ToLowerInvariant()}";
        baseNickname = baseNickname.Length > 28 ? baseNickname[..28] : baseNickname;

        var nickname = baseNickname;
        var suffix = 1;
        while (await db.Users.AnyAsync(x => x.Nickname == nickname, ct))
        {
            suffix++;
            nickname = $"{baseNickname}-{suffix}";
        }

        return nickname;
    }

    private static string BuildDisplayName(string? fullName, string nickname)
    {
        return string.IsNullOrWhiteSpace(fullName) ? nickname : fullName.Trim();
    }

    private static string? Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }

    private static string BuildDevAvatarUrl(string nickname)
    {
        return $"https://i.pravatar.cc/160?u={Uri.EscapeDataString(nickname)}";
    }
}
