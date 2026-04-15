using System.Security.Claims;

namespace FriendMap.Api.Services;

public static class CurrentUser
{
    public static Guid GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId)
            ? userId
            : Guid.Empty;
    }
}
