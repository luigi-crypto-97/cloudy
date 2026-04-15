using System.Text.Json;
using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FriendMap.Api.Services;

public class AffluenceAggregationService
{
    private readonly AppDbContext _db;
    private readonly PrivacyOptions _privacy;

    public AffluenceAggregationService(AppDbContext db, IOptions<PrivacyOptions> privacy)
    {
        _db = db;
        _privacy = privacy.Value;
    }

    public async Task<List<VenueMapMarkerDto>> GetVenueMarkersAsync(double minLat, double minLng, double maxLat, double maxLng, CancellationToken ct)
    {
        // TODO: applicare filtro spaziale vero con PostGIS/ST_Intersects.
        var venues = await _db.Venues
            .Take(200)
            .ToListAsync(ct);

        var latestSnapshots = await _db.VenueAffluenceSnapshots
            .GroupBy(x => x.VenueId)
            .Select(g => g.OrderByDescending(x => x.BucketStartUtc).First())
            .ToListAsync(ct);

        var snapshotMap = latestSnapshots.ToDictionary(x => x.VenueId);

        return venues.Select(venue =>
        {
            snapshotMap.TryGetValue(venue.Id, out var snap);
            var peopleEstimate = snap?.ActiveUsersEstimated ?? 0;
            var density = snap?.DensityLevel ?? "unknown";

            return new VenueMapMarkerDto(
                venue.Id,
                venue.Name,
                venue.Category,
                venue.Location?.Y ?? 0,
                venue.Location?.X ?? 0,
                peopleEstimate,
                density,
                ComputeBubbleIntensity(peopleEstimate),
                snap is not null && !snap.IsSuppressedForPrivacy);
        }).ToList();
    }

    public async Task<VenueDetailsDto?> GetVenueDetailsAsync(Guid venueId, CancellationToken ct)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(x => x.Id == venueId, ct);
        if (venue is null) return null;

        var latestSnapshot = await _db.VenueAffluenceSnapshots
            .Where(x => x.VenueId == venueId)
            .OrderByDescending(x => x.BucketStartUtc)
            .FirstOrDefaultAsync(ct);

        var tables = await _db.SocialTables
            .Where(x => x.VenueId == venueId && x.StartsAtUtc >= DateTimeOffset.UtcNow && x.Status == "open")
            .OrderBy(x => x.StartsAtUtc)
            .Take(20)
            .ToListAsync(ct);

        var participants = await _db.SocialTableParticipants
            .Where(x => tables.Select(t => t.Id).Contains(x.SocialTableId))
            .ToListAsync(ct);

        var intentions = await _db.VenueIntentions
            .Where(x => x.VenueId == venueId && x.EndsAtUtc >= DateTimeOffset.UtcNow)
            .GroupBy(x => new { x.StartsAtUtc, x.EndsAtUtc })
            .Select(g => new IntentionCountDto(g.Key.StartsAtUtc, g.Key.EndsAtUtc, g.Count()))
            .OrderBy(x => x.StartsAtUtc)
            .Take(12)
            .ToListAsync(ct);

        object? ages = null;
        object? genders = null;
        var showDemographics = latestSnapshot is not null && !latestSnapshot.IsSuppressedForPrivacy;

        if (showDemographics)
        {
            ages = latestSnapshot?.AggregatedAgeJson is null ? null : JsonSerializer.Deserialize<object>(latestSnapshot.AggregatedAgeJson);
            genders = latestSnapshot?.AggregatedGenderJson is null ? null : JsonSerializer.Deserialize<object>(latestSnapshot.AggregatedGenderJson);
        }

        return new VenueDetailsDto(
            venue.Id,
            venue.Name,
            venue.Category,
            venue.AddressLine,
            venue.City,
            latestSnapshot?.DensityLevel ?? "unknown",
            latestSnapshot?.ActiveUsersEstimated ?? 0,
            showDemographics,
            ages,
            genders,
            tables.Select(t =>
            {
                var group = participants.Where(p => p.SocialTableId == t.Id).ToList();
                return new SocialTableDto(
                    t.Id,
                    t.Title,
                    t.StartsAtUtc,
                    t.Capacity,
                    group.Count(x => x.Status == "requested"),
                    group.Count(x => x.Status == "accepted"),
                    t.JoinPolicy,
                    t.Status);
            }),
            intentions);
    }

    public async Task RebuildVenueSnapshotAsync(Guid venueId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var bucketMinutes = _privacy.PresenceBucketMinutes;
        var bucketStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute / bucketMinutes * bucketMinutes, 0, TimeSpan.Zero);
        var bucketEnd = bucketStart.AddMinutes(bucketMinutes);

        var activeCheckIns = await _db.VenueCheckIns
            .Where(x => x.VenueId == venueId && x.ExpiresAtUtc >= now)
            .CountAsync(ct);

        var activeIntentions = await _db.VenueIntentions
            .Where(x => x.VenueId == venueId && x.StartsAtUtc <= now.AddHours(3) && x.EndsAtUtc >= now)
            .CountAsync(ct);

        var estimated = activeCheckIns + activeIntentions;
        var suppressed = estimated < _privacy.MinimumAggregationK;

        var snapshot = await _db.VenueAffluenceSnapshots
            .FirstOrDefaultAsync(x => x.VenueId == venueId && x.BucketStartUtc == bucketStart, ct);

        if (snapshot is null)
        {
            snapshot = new VenueAffluenceSnapshot
            {
                VenueId = venueId,
                BucketStartUtc = bucketStart,
                BucketEndUtc = bucketEnd
            };
            _db.VenueAffluenceSnapshots.Add(snapshot);
        }

        snapshot.ActiveUsersEstimated = estimated;
        snapshot.DensityLevel = estimated switch
        {
            < 5 => "very_low",
            < 15 => "low",
            < 30 => "medium",
            < 60 => "high",
            _ => "very_high"
        };
        snapshot.IsSuppressedForPrivacy = suppressed;
        snapshot.AggregatedAgeJson = suppressed ? null : """{"18-24":35,"25-34":40,"35-44":15,"45+":10}""";
        snapshot.AggregatedGenderJson = suppressed ? null : """{"male":48,"female":46,"undisclosed":6}""";
        snapshot.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);
    }

    private static int ComputeBubbleIntensity(int peopleEstimate)
    {
        return peopleEstimate switch
        {
            <= 0 => 0,
            < 5 => 20,
            < 15 => 35,
            < 30 => 55,
            < 60 => 75,
            _ => 95
        };
    }
}
