using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FriendMap.Api.Services;

public sealed class FeedReentryService
{
    private readonly AppDbContext _db;
    private readonly NotificationOutboxService _outbox;
    private readonly FeedOptions _options;

    public FeedReentryService(
        AppDbContext db,
        NotificationOutboxService outbox,
        IOptions<FeedOptions> options)
    {
        _db = db;
        _outbox = outbox;
        _options = options.Value;
    }

    public async Task QueueForUserAsync(
        Guid userId,
        IEnumerable<VenueMapMarkerDto> venues,
        IEnumerable<FlareDto> flares,
        IEnumerable<SocialTableSummaryDto> tables,
        CancellationToken ct)
    {
        if (!_options.ReentryEnabled || IsQuietHours(DateTimeOffset.UtcNow))
        {
            return;
        }

        var hotVenue = venues
            .Where(x => x.PartyPulse.EnergyScore >= 78 || x.IntentRadar.GoingOut + x.IntentRadar.AlmostThere >= 3)
            .OrderByDescending(x => x.PartyPulse.EnergyScore + (x.IntentRadar.GoingOut + x.IntentRadar.AlmostThere) * 8)
            .FirstOrDefault();
        if (hotVenue is not null)
        {
            await QueueOnceAsync(
                userId,
                "venue_energy_threshold",
                hotVenue.VenueId.ToString("D"),
                "Qui si sta accendendo",
                $"{hotVenue.Name}: {hotVenue.PartyPulse.EnergyScore}% energia nel tuo giro.",
                new { type = "feed_reentry", reason = "hot_venue", venueId = hotVenue.VenueId },
                await _outbox.BuildSignedDeepLinkAsync("venue", hotVenue.VenueId, null, TimeSpan.FromHours(4), ct),
                ct);
        }

        var table = tables
            .Where(x => x.Capacity > 0 && x.AcceptedCount < x.Capacity)
            .OrderByDescending(x => (double)x.AcceptedCount / Math.Max(1, x.Capacity))
            .FirstOrDefault(x => (double)x.AcceptedCount / Math.Max(1, x.Capacity) >= 0.66);
        if (table is not null)
        {
            await QueueOnceAsync(
                userId,
                "table_almost_full",
                table.TableId.ToString("D"),
                "Tavolo quasi pieno",
                $"{table.Title}: {table.AcceptedCount}/{table.Capacity} posti da {table.VenueName}.",
                new { type = "feed_reentry", reason = "table_almost_full", tableId = table.TableId },
                await _outbox.BuildSignedDeepLinkAsync("table", table.TableId, null, TimeSpan.FromHours(6), ct),
                ct);
        }

        var expiringFlare = flares
            .Where(x => x.ExpiresAtUtc > DateTimeOffset.UtcNow && x.ExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(15))
            .OrderBy(x => x.ExpiresAtUtc)
            .FirstOrDefault();
        if (expiringFlare is not null)
        {
            await QueueOnceAsync(
                userId,
                "flare_expiring",
                expiringFlare.FlareId.ToString("D"),
                "Flare in scadenza",
                $"{expiringFlare.DisplayName ?? expiringFlare.Nickname}: hai ancora pochi minuti per rispondere.",
                new { type = "feed_reentry", reason = "flare_expiring", flareId = expiringFlare.FlareId },
                await _outbox.BuildSignedDeepLinkAsync("flare", expiringFlare.FlareId, null, TimeSpan.FromMinutes(20), ct),
                ct);
        }
    }

    public async Task QueueOnceAsync(
        Guid userId,
        string triggerType,
        string triggerKey,
        string title,
        string body,
        object payload,
        string deepLink,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var cooldownStart = now.AddMinutes(-Math.Clamp(_options.ReentryCooldownMinutes, 15, 1440));
        var state = await _db.FeedReentryNotificationStates.FirstOrDefaultAsync(x =>
            x.UserId == userId &&
            x.TriggerType == triggerType &&
            x.TriggerKey == triggerKey, ct);

        if (state is not null && state.LastSentAtUtc >= cooldownStart)
        {
            return;
        }

        if (state is null)
        {
            state = new FeedReentryNotificationState
            {
                UserId = userId,
                TriggerType = triggerType,
                TriggerKey = triggerKey
            };
            _db.FeedReentryNotificationStates.Add(state);
        }

        state.LastSentAtUtc = now;
        state.UpdatedAtUtc = now;
        await _outbox.EnqueueAsync(userId, title, body, payload, ct, deepLink);
    }

    public bool IsQuietHours(DateTimeOffset now)
    {
        var start = Math.Clamp(_options.QuietHoursStartHour, 0, 23);
        var end = Math.Clamp(_options.QuietHoursEndHour, 0, 23);
        var hour = now.LocalDateTime.Hour;
        return start == end
            ? false
            : start < end
                ? hour >= start && hour < end
                : hour >= start || hour < end;
    }
}
