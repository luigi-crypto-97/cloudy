using FriendMap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FriendMap.Api.Services;

public sealed class FeedReentryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FeedOptions _options;
    private readonly ILogger<FeedReentryBackgroundService> _logger;

    public FeedReentryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<FeedOptions> options,
        ILogger<FeedReentryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ReentryEnabled)
        {
            _logger.LogInformation("Feed re-entry worker is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Feed re-entry worker failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(30, _options.ReentryIntervalSeconds)), stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reentry = scope.ServiceProvider.GetRequiredService<FeedReentryService>();
        var outbox = scope.ServiceProvider.GetRequiredService<NotificationOutboxService>();
        var now = DateTimeOffset.UtcNow;

        if (reentry.IsQuietHours(now))
        {
            return;
        }

        var userIds = await db.NotificationDeviceTokens
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.UserId)
            .Distinct()
            .Take(150)
            .ToListAsync(ct);

        foreach (var userId in userIds)
        {
            var friendIds = await db.FriendRelations
                .AsNoTracking()
                .Where(x => x.Status == "accepted" && (x.RequesterId == userId || x.AddresseeId == userId))
                .Select(x => x.RequesterId == userId ? x.AddresseeId : x.RequesterId)
                .ToListAsync(ct);

            if (friendIds.Count == 0)
            {
                continue;
            }

            await QueueFriendsConvergingAsync(db, reentry, outbox, userId, friendIds, now, ct);
            await QueueExpiringFlareAsync(db, reentry, outbox, userId, friendIds, now, ct);
            await QueueTableAlmostFullAsync(db, reentry, outbox, userId, now, ct);
        }
    }

    private static async Task QueueFriendsConvergingAsync(
        AppDbContext db,
        FeedReentryService reentry,
        NotificationOutboxService outbox,
        Guid userId,
        List<Guid> friendIds,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var activeVenueIds = await db.VenueCheckIns
            .AsNoTracking()
            .Where(x => friendIds.Contains(x.UserId) && x.ExpiresAtUtc >= now)
            .Select(x => x.VenueId)
            .Concat(db.VenueIntentions
                .AsNoTracking()
                .Where(x => friendIds.Contains(x.UserId) && x.StartsAtUtc <= now.AddMinutes(90) && x.EndsAtUtc >= now)
                .Select(x => x.VenueId))
            .ToListAsync(ct);

        var hotVenue = activeVenueIds
            .GroupBy(x => x)
            .Where(x => x.Count() >= 3)
            .OrderByDescending(x => x.Count())
            .Select(x => new { VenueId = x.Key, Count = x.Count() })
            .FirstOrDefault();
        if (hotVenue is null)
        {
            return;
        }

        var venue = await db.Venues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == hotVenue.VenueId, ct);
        if (venue is null)
        {
            return;
        }

        await reentry.QueueOnceAsync(
            userId,
            "friends_converging",
            hotVenue.VenueId.ToString("D"),
            "Il tuo giro si sta muovendo",
            $"{hotVenue.Count} amici stanno convergendo verso {venue.Name}.",
            new { type = "feed_reentry", reason = "friends_converging", venueId = hotVenue.VenueId },
            await outbox.BuildSignedDeepLinkAsync("venue", hotVenue.VenueId, null, TimeSpan.FromHours(4), ct),
            ct);
    }

    private static async Task QueueExpiringFlareAsync(
        AppDbContext db,
        FeedReentryService reentry,
        NotificationOutboxService outbox,
        Guid userId,
        List<Guid> friendIds,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var flare = await db.FlareSignals
            .AsNoTracking()
            .Where(x => friendIds.Contains(x.UserId) && x.ExpiresAtUtc > now && x.ExpiresAtUtc <= now.AddMinutes(15))
            .OrderBy(x => x.ExpiresAtUtc)
            .FirstOrDefaultAsync(ct);
        if (flare is null)
        {
            return;
        }

        await reentry.QueueOnceAsync(
            userId,
            "flare_expiring",
            flare.Id.ToString("D"),
            "Flare in scadenza",
            "Un flare del tuo giro sta per scadere.",
            new { type = "feed_reentry", reason = "flare_expiring", flareId = flare.Id },
            await outbox.BuildSignedDeepLinkAsync("flare", flare.Id, null, TimeSpan.FromMinutes(20), ct),
            ct);
    }

    private static async Task QueueTableAlmostFullAsync(
        AppDbContext db,
        FeedReentryService reentry,
        NotificationOutboxService outbox,
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var invite = await db.SocialTableParticipants
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == "invited")
            .Join(db.SocialTables.AsNoTracking().Where(x => x.Status == "open" && x.StartsAtUtc >= now),
                p => p.SocialTableId,
                t => t.Id,
                (p, t) => t)
            .OrderBy(x => x.StartsAtUtc)
            .FirstOrDefaultAsync(ct);
        if (invite is null)
        {
            return;
        }

        var accepted = await db.SocialTableParticipants
            .AsNoTracking()
            .CountAsync(x => x.SocialTableId == invite.Id && x.Status == "accepted", ct);
        if (invite.Capacity <= 0 || (double)accepted / invite.Capacity < 0.66)
        {
            return;
        }

        await reentry.QueueOnceAsync(
            userId,
            "table_almost_full",
            invite.Id.ToString("D"),
            "Tavolo quasi pieno",
            $"{invite.Title}: {accepted}/{invite.Capacity} posti.",
            new { type = "feed_reentry", reason = "table_almost_full", tableId = invite.Id },
            await outbox.BuildSignedDeepLinkAsync("table", invite.Id, null, TimeSpan.FromHours(6), ct),
            ct);
    }
}
