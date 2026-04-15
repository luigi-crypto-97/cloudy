using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FriendMap.Api.Services;

public class VenueAnalyticsService
{
    private readonly AppDbContext _db;

    public VenueAnalyticsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardOverviewDto> GetOverviewAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return new DashboardOverviewDto(
            ActiveUsers24h: await _db.Users.CountAsync(x => x.Status == "active", ct),
            CheckInsActive: await _db.VenueCheckIns.CountAsync(x => x.ExpiresAtUtc >= now, ct),
            IntentionsActive: await _db.VenueIntentions.CountAsync(x => x.EndsAtUtc >= now, ct),
            OpenReports: await _db.ModerationReports.CountAsync(x => x.Status == "open", ct),
            ActiveTables: await _db.SocialTables.CountAsync(x => x.Status == "open" && x.StartsAtUtc >= now, ct)
        );
    }
}
