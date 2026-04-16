using System.Text.Json;
using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FriendMap.Api.Services;

public class AffluenceAggregationService
{
    private const double EarthRadiusMeters = 6_371_000d;
    private readonly AppDbContext _db;
    private readonly PrivacyOptions _privacy;

    public AffluenceAggregationService(AppDbContext db, IOptions<PrivacyOptions> privacy)
    {
        _db = db;
        _privacy = privacy.Value;
    }

    public async Task<List<VenueMapMarkerDto>> GetVenueMarkersAsync(
        double minLat,
        double minLng,
        double maxLat,
        double maxLng,
        Guid viewerUserId,
        string? query,
        string? category,
        bool openNowOnly,
        double? centerLat,
        double? centerLng,
        double? maxDistanceKm,
        CancellationToken ct)
    {
        var south = Math.Min(minLat, maxLat);
        var north = Math.Max(minLat, maxLat);
        var west = Math.Min(minLng, maxLng);
        var east = Math.Max(minLng, maxLng);
        var now = DateTimeOffset.UtcNow;

        var venues = await _db.Venues
            .FromSqlInterpolated($"""
                SELECT *
                FROM venues
                WHERE visibility_status = 'public'
                  AND location IS NOT NULL
                  AND ST_Intersects(location, ST_MakeEnvelope({west}, {south}, {east}, {north}, 4326)::geography)
                ORDER BY name
                """)
            .Take(300)
            .ToListAsync(ct);

        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim().ToLowerInvariant();

        venues = venues
            .Where(venue =>
            {
                if (!string.IsNullOrWhiteSpace(normalizedQuery))
                {
                    var haystack = $"{venue.Name} {venue.Category} {venue.AddressLine} {venue.City}";
                    if (!haystack.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                if (!MatchesCategoryFilter(venue.Category, normalizedCategory))
                {
                    return false;
                }

                if (openNowOnly && !IsVenueOpenNow(venue, DateTimeOffset.Now))
                {
                    return false;
                }

                if (maxDistanceKm is > 0 &&
                    centerLat is double lat &&
                    centerLng is double lng &&
                    venue.Location is not null &&
                    HaversineDistanceMeters(lat, lng, venue.Location.Y, venue.Location.X) > maxDistanceKm.Value * 1000d)
                {
                    return false;
                }

                return true;
            })
            .ToList();

        var venueIds = venues.Select(x => x.Id).ToList();

        var latestSnapshots = await _db.VenueAffluenceSnapshots
            .GroupBy(x => x.VenueId)
            .Select(g => g.OrderByDescending(x => x.BucketStartUtc).First())
            .ToListAsync(ct);

        var snapshotMap = latestSnapshots.ToDictionary(x => x.VenueId);

        var activeCheckIns = await _db.VenueCheckIns
            .Where(x => venueIds.Contains(x.VenueId) && x.ExpiresAtUtc >= now)
            .ToListAsync(ct);

        var activeIntentions = await _db.VenueIntentions
            .Where(x => venueIds.Contains(x.VenueId) && x.EndsAtUtc >= now && x.StartsAtUtc <= now.AddHours(3))
            .ToListAsync(ct);

        var openTables = await _db.SocialTables
            .Where(x => venueIds.Contains(x.VenueId) && x.Status == "open" && x.StartsAtUtc >= now)
            .ToListAsync(ct);

        var openTableIds = openTables.Select(x => x.Id).ToList();
        var tableParticipants = openTableIds.Count == 0
            ? new List<SocialTableParticipant>()
            : await _db.SocialTableParticipants
                .Where(x => openTableIds.Contains(x.SocialTableId) && x.Status == "accepted")
                .ToListAsync(ct);

        var friendIds = new HashSet<Guid>();
        if (viewerUserId != Guid.Empty)
        {
            var relations = await _db.FriendRelations
                .Where(x => x.Status == "accepted" && (x.RequesterId == viewerUserId || x.AddresseeId == viewerUserId))
                .ToListAsync(ct);

            foreach (var relation in relations)
            {
                friendIds.Add(relation.RequesterId == viewerUserId ? relation.AddresseeId : relation.RequesterId);
            }
        }

        var socialUserIds = activeCheckIns.Select(x => x.UserId)
            .Concat(activeIntentions.Select(x => x.UserId))
            .Concat(openTables.Select(x => x.HostUserId))
            .Concat(tableParticipants.Select(x => x.UserId))
            .Distinct()
            .ToList();

        var socialUsers = socialUserIds.Count == 0
            ? new Dictionary<Guid, AppUser>()
            : await _db.Users
                .Where(x => socialUserIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

        var presenceByVenue = venueIds.ToDictionary(x => x, _ => new HashSet<Guid>());
        var previewByVenue = venueIds.ToDictionary(x => x, _ => new HashSet<Guid>());
        foreach (var checkIn in activeCheckIns)
        {
            presenceByVenue[checkIn.VenueId].Add(checkIn.UserId);
            if (CanRevealIdentity(checkIn.UserId, viewerUserId, friendIds, socialUsers, revealIntentions: false))
            {
                previewByVenue[checkIn.VenueId].Add(checkIn.UserId);
            }
        }

        foreach (var intention in activeIntentions)
        {
            presenceByVenue[intention.VenueId].Add(intention.UserId);
            if (CanRevealIdentity(intention.UserId, viewerUserId, friendIds, socialUsers, revealIntentions: true))
            {
                previewByVenue[intention.VenueId].Add(intention.UserId);
            }
        }

        var tableVenueMap = openTables.ToDictionary(x => x.Id, x => x.VenueId);
        foreach (var table in openTables)
        {
            presenceByVenue[table.VenueId].Add(table.HostUserId);
            if (CanRevealIdentity(table.HostUserId, viewerUserId, friendIds, socialUsers, revealIntentions: false))
            {
                previewByVenue[table.VenueId].Add(table.HostUserId);
            }
        }

        foreach (var participant in tableParticipants)
        {
            if (tableVenueMap.TryGetValue(participant.SocialTableId, out var venueId))
            {
                presenceByVenue[venueId].Add(participant.UserId);
                if (CanRevealIdentity(participant.UserId, viewerUserId, friendIds, socialUsers, revealIntentions: false))
                {
                    previewByVenue[venueId].Add(participant.UserId);
                }
            }
        }

        return venues.Select(venue =>
        {
            snapshotMap.TryGetValue(venue.Id, out var snap);
            var peopleEstimate = snap?.ActiveUsersEstimated ?? 0;
            var density = snap?.DensityLevel ?? "unknown";
            var venueCheckIns = activeCheckIns.Count(x => x.VenueId == venue.Id);
            var venueIntentions = activeIntentions.Count(x => x.VenueId == venue.Id);
            var venueTables = openTables.Count(x => x.VenueId == venue.Id);

            var previewUserIds = previewByVenue.TryGetValue(venue.Id, out var presentIds)
                ? presentIds
                    .OrderByDescending(id => friendIds.Contains(id))
                    .ThenBy(id => socialUsers.TryGetValue(id, out var user) ? user.DisplayName ?? user.Nickname : id.ToString())
                    .Take(3)
                    .ToList()
                : new List<Guid>();

            return new VenueMapMarkerDto(
                venue.Id,
                venue.Name,
                venue.Category,
                venue.AddressLine,
                venue.City,
                venue.Location?.Y ?? 0,
                venue.Location?.X ?? 0,
                IsVenueOpenNow(venue, DateTimeOffset.Now),
                peopleEstimate,
                density,
                ComputeBubbleIntensity(peopleEstimate),
                snap is not null && !snap.IsSuppressedForPrivacy,
                venueCheckIns,
                venueIntentions,
                venueTables,
                previewUserIds
                    .Where(id => socialUsers.ContainsKey(id))
                    .Select(id =>
                    {
                        var user = socialUsers[id];
                        return new PresencePreviewDto(
                            user.Id,
                            string.IsNullOrWhiteSpace(user.DisplayName) ? user.Nickname : user.DisplayName!,
                            user.Nickname,
                            user.AvatarUrl);
                    })
                    .ToList());
        }).ToList();
    }

    public async Task<VenueMapLayerDto> GetVenueMapLayerAsync(
        double minLat,
        double minLng,
        double maxLat,
        double maxLng,
        Guid viewerUserId,
        string? query,
        string? category,
        bool openNowOnly,
        double? centerLat,
        double? centerLng,
        double? maxDistanceKm,
        CancellationToken ct)
    {
        var markers = await GetVenueMarkersAsync(
            minLat,
            minLng,
            maxLat,
            maxLng,
            viewerUserId,
            query,
            category,
            openNowOnly,
            centerLat,
            centerLng,
            maxDistanceKm,
            ct);
        var areas = BuildVenueAreas(markers, minLat, minLng, maxLat, maxLng);
        return new VenueMapLayerDto(markers, areas);
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

    private List<VenueMapAreaDto> BuildVenueAreas(List<VenueMapMarkerDto> markers, double minLat, double minLng, double maxLat, double maxLng)
    {
        if (markers.Count == 0)
        {
            return new List<VenueMapAreaDto>();
        }

        var latitudeSpan = Math.Abs(maxLat - minLat);
        var longitudeSpan = Math.Abs(maxLng - minLng);
        var clusterDistanceMeters = ResolveAreaClusterDistanceMeters(latitudeSpan, longitudeSpan);
        var groupedMarkers = GroupMarkersByProximity(markers, clusterDistanceMeters);

        return groupedMarkers
            .Select(group =>
            {
                var areaMarkers = group.ToList();
                var uniquePresence = areaMarkers
                    .SelectMany(x => x.PresencePreview)
                    .GroupBy(x => x.UserId)
                    .Select(x => x.First())
                    .ToList();
                var centroidLatitude = areaMarkers.Average(x => x.Latitude);
                var centroidLongitude = areaMarkers.Average(x => x.Longitude);
                var peopleCount = areaMarkers.Sum(GetMarkerPeopleCount);
                var densityLevel = ResolveAreaDensityLevel(peopleCount);
                var bubbleIntensity = ComputeBubbleIntensity(peopleCount);
                var polygon = BuildAreaPolygon(areaMarkers, bubbleIntensity);

                return new VenueMapAreaDto(
                    BuildAreaKey(areaMarkers),
                    BuildAreaLabel(areaMarkers),
                    centroidLatitude,
                    centroidLongitude,
                    peopleCount,
                    densityLevel,
                    bubbleIntensity,
                    areaMarkers.Count,
                    areaMarkers.Sum(x => x.ActiveCheckIns),
                    areaMarkers.Sum(x => x.ActiveIntentions),
                    areaMarkers.Sum(x => x.OpenTables),
                    uniquePresence.Count,
                    areaMarkers.Select(x => x.VenueId).ToList(),
                    polygon,
                    uniquePresence.Take(4).ToList());
            })
            .OrderByDescending(x => x.PeopleCount)
            .ThenBy(x => x.Label)
            .ToList();
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

    private static int GetMarkerPeopleCount(VenueMapMarkerDto marker)
    {
        return Math.Max(marker.PeopleEstimate, marker.ActiveCheckIns + marker.ActiveIntentions);
    }

    private static double ResolveAreaClusterDistanceMeters(double latitudeSpan, double longitudeSpan)
    {
        var normalizedSpan = Math.Max(latitudeSpan, longitudeSpan);
        return normalizedSpan switch
        {
            < 0.018d => 0d,
            < 0.040d => 240d,
            < 0.075d => 360d,
            < 0.140d => 520d,
            _ => 760d
        };
    }

    private static List<List<VenueMapMarkerDto>> GroupMarkersByProximity(List<VenueMapMarkerDto> markers, double maxDistanceMeters)
    {
        if (maxDistanceMeters <= 0d || markers.Count <= 1)
        {
            return markers.Select(x => new List<VenueMapMarkerDto> { x }).ToList();
        }

        var visited = new bool[markers.Count];
        var groups = new List<List<VenueMapMarkerDto>>();

        for (var i = 0; i < markers.Count; i++)
        {
            if (visited[i])
            {
                continue;
            }

            var queue = new Queue<int>();
            var component = new List<VenueMapMarkerDto>();
            visited[i] = true;
            queue.Enqueue(i);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentMarker = markers[current];
                component.Add(currentMarker);

                for (var candidate = 0; candidate < markers.Count; candidate++)
                {
                    if (visited[candidate])
                    {
                        continue;
                    }

                    var candidateMarker = markers[candidate];
                    if (HaversineDistanceMeters(
                            currentMarker.Latitude,
                            currentMarker.Longitude,
                            candidateMarker.Latitude,
                            candidateMarker.Longitude) > maxDistanceMeters)
                    {
                        continue;
                    }

                    visited[candidate] = true;
                    queue.Enqueue(candidate);
                }
            }

            groups.Add(component);
        }

        return groups;
    }

    private static List<GeoPointDto> BuildAreaPolygon(List<VenueMapMarkerDto> markers, int bubbleIntensity)
    {
        var points = new List<AreaPoint>();
        foreach (var marker in markers)
        {
            var radiusMeters = Math.Max(110d, 92d + Math.Max(bubbleIntensity, marker.BubbleIntensity) * 4.1d);
            points.AddRange(CreateBufferedPoints(marker.Latitude, marker.Longitude, radiusMeters, 8));
        }

        var hull = ComputeConvexHull(points);
        if (hull.Count < 3)
        {
            var centerLatitude = markers.Average(x => x.Latitude);
            var centerLongitude = markers.Average(x => x.Longitude);
            hull = CreateBufferedPoints(centerLatitude, centerLongitude, 160d, 8);
        }

        return hull.Select(x => new GeoPointDto(x.Latitude, x.Longitude)).ToList();
    }

    private static List<AreaPoint> CreateBufferedPoints(double latitude, double longitude, double radiusMeters, int segments)
    {
        var points = new List<AreaPoint>(segments);
        for (var i = 0; i < segments; i++)
        {
            var angle = 2d * Math.PI * i / segments;
            var eastMeters = Math.Cos(angle) * radiusMeters;
            var northMeters = Math.Sin(angle) * radiusMeters;
            points.Add(OffsetPoint(latitude, longitude, northMeters, eastMeters));
        }

        return points;
    }

    private static AreaPoint OffsetPoint(double latitude, double longitude, double northMeters, double eastMeters)
    {
        var latOffset = northMeters / 111_320d;
        var lngScale = Math.Cos(latitude * Math.PI / 180d);
        var lngOffset = eastMeters / (111_320d * Math.Max(0.2d, lngScale));
        return new AreaPoint(latitude + latOffset, longitude + lngOffset);
    }

    private static List<AreaPoint> ComputeConvexHull(List<AreaPoint> points)
    {
        if (points.Count <= 3)
        {
            return points
                .Distinct()
                .ToList();
        }

        var sorted = points
            .Distinct()
            .OrderBy(x => x.Longitude)
            .ThenBy(x => x.Latitude)
            .ToList();

        if (sorted.Count <= 3)
        {
            return sorted;
        }

        var lower = new List<AreaPoint>();
        foreach (var point in sorted)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], point) <= 0d)
            {
                lower.RemoveAt(lower.Count - 1);
            }

            lower.Add(point);
        }

        var upper = new List<AreaPoint>();
        for (var i = sorted.Count - 1; i >= 0; i--)
        {
            var point = sorted[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], point) <= 0d)
            {
                upper.RemoveAt(upper.Count - 1);
            }

            upper.Add(point);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        return lower.Concat(upper).ToList();
    }

    private static double Cross(AreaPoint origin, AreaPoint a, AreaPoint b)
    {
        return (a.Longitude - origin.Longitude) * (b.Latitude - origin.Latitude) -
               (a.Latitude - origin.Latitude) * (b.Longitude - origin.Longitude);
    }

    private static double HaversineDistanceMeters(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
    {
        var lat1 = DegreesToRadians(latitudeA);
        var lat2 = DegreesToRadians(latitudeB);
        var deltaLat = DegreesToRadians(latitudeB - latitudeA);
        var deltaLng = DegreesToRadians(longitudeB - longitudeA);

        var sinLat = Math.Sin(deltaLat / 2d);
        var sinLng = Math.Sin(deltaLng / 2d);
        var a = sinLat * sinLat +
                Math.Cos(lat1) * Math.Cos(lat2) *
                sinLng * sinLng;
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double value)
    {
        return value * Math.PI / 180d;
    }

    private static string ResolveAreaDensityLevel(int peopleCount)
    {
        return peopleCount switch
        {
            < 5 => "very_low",
            < 15 => "low",
            < 30 => "medium",
            < 60 => "high",
            _ => "very_high"
        };
    }

    private static string BuildAreaKey(IEnumerable<VenueMapMarkerDto> markers)
    {
        return string.Join('|', markers.Select(x => x.VenueId.ToString("N")).OrderBy(x => x, StringComparer.Ordinal));
    }

    private static string BuildAreaLabel(IEnumerable<VenueMapMarkerDto> markers)
    {
        var lead = markers
            .OrderByDescending(GetMarkerPeopleCount)
            .ThenByDescending(x => x.OpenTables)
            .First();

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bar", "club", "cafe", "cafè", "demo", "social", "ristorante", "bistrot", "pub", "the"
        };

        var parts = lead.Name
            .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !stopWords.Contains(x))
            .Take(2)
            .ToList();

        if (parts.Count == 0)
        {
            parts = lead.Name
                .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(2)
                .ToList();
        }

        return parts.Count == 0 ? "Area live" : string.Join(" ", parts);
    }

    private readonly record struct AreaPoint(double Latitude, double Longitude);

    private static bool CanRevealIdentity(
        Guid userId,
        Guid viewerUserId,
        HashSet<Guid> friendIds,
        Dictionary<Guid, AppUser> users,
        bool revealIntentions)
    {
        if (!users.TryGetValue(userId, out var user))
        {
            return false;
        }

        if (user.IsGhostModeEnabled)
        {
            return false;
        }

        if (viewerUserId == Guid.Empty)
        {
            return false;
        }

        if (viewerUserId == userId)
        {
            return true;
        }

        if (!friendIds.Contains(userId))
        {
            return false;
        }

        return revealIntentions
            ? user.ShareIntentionsWithFriends
            : user.SharePresenceWithFriends;
    }

    private static bool IsVenueOpenNow(Venue venue, DateTimeOffset localNow)
    {
        var hour = localNow.Hour;
        var category = venue.Category.Trim().ToLowerInvariant();

        return category switch
        {
            "bar" or "pub" or "club" => hour is >= 18 or <= 2,
            "restaurant" or "ristorante" or "bistrot" => hour is >= 12 and <= 15 || hour is >= 19 and <= 23,
            "cafe" or "cafè" => hour is >= 7 and <= 19,
            _ => hour is >= 10 and <= 23
        };
    }

    private static bool MatchesCategoryFilter(string venueCategory, string? categoryFilter)
    {
        if (string.IsNullOrWhiteSpace(categoryFilter))
        {
            return true;
        }

        var normalizedVenueCategory = venueCategory.Trim().ToLowerInvariant();
        var normalizedFilter = categoryFilter.Trim().ToLowerInvariant();

        return normalizedFilter switch
        {
            "all" => true,
            "bar" => normalizedVenueCategory.Contains("bar", StringComparison.OrdinalIgnoreCase)
                || normalizedVenueCategory.Contains("pub", StringComparison.OrdinalIgnoreCase)
                || normalizedVenueCategory.Contains("club", StringComparison.OrdinalIgnoreCase),
            "food" => normalizedVenueCategory.Contains("restaurant", StringComparison.OrdinalIgnoreCase)
                || normalizedVenueCategory.Contains("ristor", StringComparison.OrdinalIgnoreCase)
                || normalizedVenueCategory.Contains("bistrot", StringComparison.OrdinalIgnoreCase)
                || normalizedVenueCategory.Contains("pizzer", StringComparison.OrdinalIgnoreCase)
                || normalizedVenueCategory.Contains("trattor", StringComparison.OrdinalIgnoreCase)
                || normalizedVenueCategory.Contains("cafe", StringComparison.OrdinalIgnoreCase)
                || normalizedVenueCategory.Contains("caf", StringComparison.OrdinalIgnoreCase),
            _ => normalizedVenueCategory.Equals(normalizedFilter, StringComparison.OrdinalIgnoreCase)
        };
    }
}
