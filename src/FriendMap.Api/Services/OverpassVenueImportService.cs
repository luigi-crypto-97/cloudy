using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FriendMap.Api.Contracts;
using FriendMap.Api.Data;
using FriendMap.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace FriendMap.Api.Services;

public class OverpassVenueImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _db;
    private readonly OverpassOptions _options;

    public OverpassVenueImportService(
        HttpClient httpClient,
        IHttpClientFactory httpClientFactory,
        AppDbContext db,
        IOptions<OverpassOptions> options)
    {
        _httpClient = httpClient;
        _httpClientFactory = httpClientFactory;
        _db = db;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<VenueImportCandidateDto>> PreviewAsync(VenueImportRequest request, CancellationToken ct)
    {
        ValidateRequest(request);

        var area = await ResolveAreaAsync(request, ct);
        var query = BuildQuery(request, area);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["data"] = query
        });

        using var response = await SendAsync(content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Overpass search failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");

        var overpass = JsonSerializer.Deserialize<OverpassResponse>(body, JsonOptions) ?? new OverpassResponse();
        var maxResults = Math.Clamp(request.Limit, 1, 500);
        var candidates = overpass.Elements
            .Select(ToCandidate)
            .Where(x => x is not null)
            .Select(x => x!)
            .Where(x => MatchesNameFilter(x, request.Query))
            .GroupBy(x => NormalizeName(x.Name))
            .Select(x => x.First())
            .Take(maxResults)
            .ToList();

        var providerIds = candidates.Select(x => x.ExternalProviderId).Distinct(StringComparer.Ordinal).ToArray();
        if (providerIds.Length == 0)
            return candidates;

        var existingByProviderId = await _db.Venues
            .AsNoTracking()
            .Where(x => providerIds.Contains(x.ExternalProviderId))
            .Select(x => new { x.Id, x.ExternalProviderId })
            .ToDictionaryAsync(x => x.ExternalProviderId, x => x.Id, StringComparer.Ordinal, ct);

        var existingNearby = await LoadExistingNearbyAsync(candidates, ct);

        return candidates
            .Select(x => existingByProviderId.TryGetValue(x.ExternalProviderId, out var venueId)
                ? x with { AlreadyExists = true, ExistingVenueId = venueId }
                : TryFindNearbyDuplicate(x, existingNearby, out var nearbyVenueId)
                    ? x with { AlreadyExists = true, ExistingVenueId = nearbyVenueId }
                : x)
            .ToList();
    }

    public async Task<VenueImportResultDto> ImportAsync(VenueImportRequest request, CancellationToken ct)
    {
        var candidates = await PreviewAsync(request, ct);
        var imported = new List<AdminVenueDto>();
        var created = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var candidate in candidates)
        {
            var venue = await _db.Venues.FirstOrDefaultAsync(x => x.ExternalProviderId == candidate.ExternalProviderId, ct);
            venue ??= await FindExistingNearbyAsync(candidate, ct);
            if (venue is null)
            {
                venue = new Venue();
                ApplyCandidate(venue, candidate, request.VisibilityStatus);
                _db.Venues.Add(venue);
                created++;
            }
            else if (request.UpdateExisting && !venue.IsClaimed)
            {
                ApplyCandidate(venue, candidate, request.VisibilityStatus);
                venue.UpdatedAtUtc = DateTimeOffset.UtcNow;
                updated++;
            }
            else
            {
                skipped++;
                continue;
            }

            imported.Add(ToAdminVenueDto(venue));
        }

        await _db.SaveChangesAsync(ct);
        return new VenueImportResultDto(candidates.Count, created, updated, skipped, imported);
    }

    private async Task<SearchArea> ResolveAreaAsync(VenueImportRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Area))
            return SearchArea.Around(request.Latitude, request.Longitude, request.RadiusMeters);

        var client = _httpClientFactory.CreateClient("nominatim");
        var countryCodes = NormalizeCountryCodes(request.CountryCodesCsv);
        var uri = $"search?format=jsonv2&limit=1&q={Uri.EscapeDataString(request.Area.Trim())}";
        if (!string.IsNullOrWhiteSpace(countryCodes))
            uri += $"&countrycodes={Uri.EscapeDataString(countryCodes)}";

        using var response = await client.GetAsync(uri, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nominatim search failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");

        var results = JsonSerializer.Deserialize<List<NominatimResult>>(body, JsonOptions) ?? new List<NominatimResult>();
        var first = results.FirstOrDefault();
        if (first is null)
            throw new InvalidOperationException($"Area non trovata: {request.Area}");

        if (first.BoundingBox is { Length: 4 } bbox &&
            double.TryParse(bbox[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var south) &&
            double.TryParse(bbox[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var north) &&
            double.TryParse(bbox[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var west) &&
            double.TryParse(bbox[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var east))
        {
            return SearchArea.BoundingBox(south, west, north, east);
        }

        if (double.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
            double.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
        {
            return SearchArea.Around(lat, lon, request.RadiusMeters);
        }

        throw new InvalidOperationException($"Area trovata ma senza coordinate utilizzabili: {request.Area}");
    }

    private string BuildQuery(VenueImportRequest request, SearchArea area)
    {
        var limit = Math.Clamp(request.Limit * 3, 25, 1500).ToString(CultureInfo.InvariantCulture);
        var timeout = Math.Clamp(_options.TimeoutSeconds, 5, 180).ToString(CultureInfo.InvariantCulture);
        var categories = NormalizeCategories(request.CategoriesCsv);
        var selectorSuffix = area.ToOverpassSelector();

        var amenityPattern = BuildPattern(categories.Amenity);
        var tourismPattern = BuildPattern(categories.Tourism);
        var leisurePattern = BuildPattern(categories.Leisure);

        return $$"""
                 [out:json][timeout:{{timeout}}];
                 (
                   nwr["amenity"~"^({{amenityPattern}})$"]{{selectorSuffix}};
                   nwr["shop"~"^(bakery|coffee|confectionery|pastry|wine|alcohol|beverages|deli)$"]{{selectorSuffix}};
                   nwr["tourism"~"^({{tourismPattern}})$"]{{selectorSuffix}};
                   nwr["leisure"~"^({{leisurePattern}})$"]{{selectorSuffix}};
                 );
                 out center tags qt {{limit}};
                 """;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpContent content, CancellationToken ct)
    {
        try
        {
            return await _httpClient.PostAsync("interpreter", content, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException("Timeout durante la chiamata a Overpass. Riduci raggio/limite o riprova piu tardi.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Overpass non e raggiungibile. Dettaglio: {ex.Message}", ex);
        }
    }

    private static VenueImportCandidateDto? ToCandidate(OverpassElement element)
    {
        if (element.Tags is null ||
            !element.Tags.TryGetValue("name", out var name) ||
            string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var latitude = element.Lat ?? element.Center?.Lat;
        var longitude = element.Lon ?? element.Center?.Lon;
        if (latitude is null || longitude is null)
            return null;

        var category = GetCategory(element.Tags);
        var address = GetAddress(element.Tags);
        var city = GetTag(element.Tags, "addr:city", "addr:town", "addr:village", "is_in:city") ?? string.Empty;
        var country = GetTag(element.Tags, "addr:country") ?? "IT";
        var phone = GetTag(element.Tags, "phone", "contact:phone");
        var website = GetTag(element.Tags, "website", "contact:website");
        var hours = GetTag(element.Tags, "opening_hours");
        var tagsCsv = string.Join(",", BuildTags(element.Tags).Distinct(StringComparer.OrdinalIgnoreCase));

        return new VenueImportCandidateDto(
            $"osm:{element.Type}:{element.Id}",
            name.Trim(),
            category,
            address,
            city.Trim(),
            country.Trim().ToUpperInvariant(),
            NullIfWhiteSpace(phone),
            NullIfWhiteSpace(website),
            NullIfWhiteSpace(hours),
            null,
            "Import automatico da OpenStreetMap. Verificare i dati prima della pubblicazione.",
            NullIfWhiteSpace(tagsCsv),
            latitude.Value,
            longitude.Value,
            false,
            null);
    }

    private static void ApplyCandidate(Venue venue, VenueImportCandidateDto candidate, string visibilityStatus)
    {
        venue.ExternalProviderId = candidate.ExternalProviderId;
        venue.Name = candidate.Name;
        venue.Category = candidate.Category;
        venue.AddressLine = candidate.AddressLine;
        venue.City = candidate.City;
        venue.CountryCode = string.IsNullOrWhiteSpace(candidate.CountryCode) ? "IT" : candidate.CountryCode;
        venue.PhoneNumber = candidate.PhoneNumber;
        venue.WebsiteUrl = candidate.WebsiteUrl;
        venue.HoursSummary = candidate.HoursSummary;
        venue.CoverImageUrl = candidate.CoverImageUrl;
        venue.Description = candidate.Description;
        venue.TagsCsv = candidate.TagsCsv;
        venue.Location = new Point(candidate.Longitude, candidate.Latitude) { SRID = 4326 };
        venue.VisibilityStatus = string.IsNullOrWhiteSpace(visibilityStatus) ? "review" : visibilityStatus.Trim();
    }

    private static AdminVenueDto ToAdminVenueDto(Venue venue)
    {
        return new AdminVenueDto(
            venue.Id,
            venue.ExternalProviderId,
            venue.Name,
            venue.Category,
            venue.AddressLine,
            venue.City,
            venue.CountryCode,
            venue.PhoneNumber,
            venue.WebsiteUrl,
            venue.HoursSummary,
            venue.CoverImageUrl,
            venue.Description,
            venue.TagsCsv,
            venue.Location?.Y,
            venue.Location?.X,
            venue.IsClaimed,
            venue.VisibilityStatus);
    }

    private static bool MatchesNameFilter(VenueImportCandidateDto candidate, string? query)
    {
        return string.IsNullOrWhiteSpace(query) ||
               candidate.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               candidate.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               (candidate.TagsCsv?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string GetCategory(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.TryGetValue("amenity", out var amenity)) return MapCategory(amenity);
        if (tags.TryGetValue("shop", out var shop)) return MapShopCategory(shop);
        if (tags.TryGetValue("tourism", out var tourism)) return MapCategory(tourism);
        if (tags.TryGetValue("leisure", out var leisure)) return MapCategory(leisure);
        if (tags.TryGetValue("office", out var office)) return $"office:{office}";
        if (tags.TryGetValue("craft", out var craft)) return $"craft:{craft}";
        return "venue";
    }

    private static string MapCategory(string value)
    {
        return value switch
        {
            "fast_food" => "fast_food",
            "fitness_centre" or "sports_centre" => "fitness",
            "guest_house" or "hostel" or "hotel" or "motel" => "hotel",
            "adult_gaming_centre" or "amusement_arcade" or "casino" or "gambling" => "casino",
            _ => value
        };
    }

    private static string MapShopCategory(string value)
    {
        return value switch
        {
            "bakery" or "confectionery" or "pastry" => "bakery",
            "coffee" => "cafe",
            "wine" or "alcohol" or "beverages" => "bar",
            "deli" => "food",
            _ => $"shop:{value}"
        };
    }

    private static string GetAddress(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.TryGetValue("addr:full", out var full) && !string.IsNullOrWhiteSpace(full))
            return full.Trim();

        var street = GetTag(tags, "addr:street", "addr:place");
        var houseNumber = GetTag(tags, "addr:housenumber");
        return string.Join(" ", new[] { street, houseNumber }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
    }

    private static IEnumerable<string> BuildTags(IReadOnlyDictionary<string, string> tags)
    {
        foreach (var key in new[] { "amenity", "shop", "tourism", "leisure", "office", "craft", "cuisine" })
        {
            if (tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    private static string? GetTag(IReadOnlyDictionary<string, string> tags, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static CategorySets NormalizeCategories(string? categoriesCsv)
    {
        var defaults = new CategorySets(
            new[] { "restaurant", "bar", "cafe", "pub", "fast_food", "biergarten", "ice_cream", "casino", "gambling", "nightclub", "food_court" },
            Array.Empty<string>(),
            new[] { "fitness_centre", "sports_centre", "adult_gaming_centre", "amusement_arcade", "casino", "bowling_alley", "escape_game" });

        if (string.IsNullOrWhiteSpace(categoriesCsv))
            return defaults;

        var requested = categoriesCsv
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new CategorySets(
            defaults.Amenity.Where(requested.Contains).ToArray(),
            defaults.Tourism.Where(requested.Contains).ToArray(),
            defaults.Leisure.Where(requested.Contains).ToArray());
    }

    private static string BuildPattern(IEnumerable<string> values)
    {
        var pattern = string.Join("|", values.Select(x => x.Replace("|", string.Empty, StringComparison.Ordinal)));
        return string.IsNullOrWhiteSpace(pattern) ? "__none__" : pattern;
    }

    private static void ValidateRequest(VenueImportRequest request)
    {
        if (request.Latitude is < -90 or > 90) throw new ArgumentException("latitude must be between -90 and 90.");
        if (request.Longitude is < -180 or > 180) throw new ArgumentException("longitude must be between -180 and 180.");
        if (request.RadiusMeters is < 1 or > 50000) throw new ArgumentException("radiusMeters must be between 1 and 50000.");
        if (request.Limit is < 1 or > 500) throw new ArgumentException("limit must be between 1 and 500.");
    }

    private async Task<List<NearbyVenueLookup>> LoadExistingNearbyAsync(IReadOnlyList<VenueImportCandidateDto> candidates, CancellationToken ct)
    {
        var minLat = candidates.Min(x => x.Latitude) - 0.01;
        var maxLat = candidates.Max(x => x.Latitude) + 0.01;
        var minLng = candidates.Min(x => x.Longitude) - 0.01;
        var maxLng = candidates.Max(x => x.Longitude) + 0.01;

        return await _db.Database
            .SqlQuery<NearbyVenueLookup>(
                $"""
                 SELECT id AS "Id",
                        name AS "Name",
                        ST_Y(location::geometry) AS "Latitude",
                        ST_X(location::geometry) AS "Longitude"
                 FROM venues
                 WHERE location IS NOT NULL
                   AND ST_Y(location::geometry) BETWEEN {minLat} AND {maxLat}
                   AND ST_X(location::geometry) BETWEEN {minLng} AND {maxLng}
                 """)
            .ToListAsync(ct);
    }

    private async Task<Venue?> FindExistingNearbyAsync(VenueImportCandidateDto candidate, CancellationToken ct)
    {
        var normalizedName = NormalizeName(candidate.Name);
        var minLat = candidate.Latitude - 0.002;
        var maxLat = candidate.Latitude + 0.002;
        var minLng = candidate.Longitude - 0.002;
        var maxLng = candidate.Longitude + 0.002;

        var venues = await LoadExistingNearbyAsync(
            new[] { candidate with { Latitude = (minLat + maxLat) / 2, Longitude = (minLng + maxLng) / 2 } },
            ct);

        var duplicate = venues.FirstOrDefault(x =>
            NormalizeName(x.Name) == normalizedName &&
            DistanceMeters(candidate.Latitude, candidate.Longitude, x.Latitude, x.Longitude) <= 80);

        return duplicate is null
            ? null
            : await _db.Venues.FirstOrDefaultAsync(x => x.Id == duplicate.Id, ct);
    }

    private static bool TryFindNearbyDuplicate(VenueImportCandidateDto candidate, IReadOnlyList<NearbyVenueLookup> venues, out Guid venueId)
    {
        var normalizedName = NormalizeName(candidate.Name);
        foreach (var venue in venues)
        {
            if (NormalizeName(venue.Name) == normalizedName &&
                DistanceMeters(candidate.Latitude, candidate.Longitude, venue.Latitude, venue.Longitude) <= 80)
            {
                venueId = venue.Id;
                return true;
            }
        }

        venueId = Guid.Empty;
        return false;
    }

    private static string NormalizeName(string value)
    {
        return new string(value
                .Trim()
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());
    }

    private static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusMeters = 6371000d;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLng = DegreesToRadians(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeCountryCodes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return string.Join(",", value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Where(x => x.Length == 2)
            .Distinct(StringComparer.Ordinal));
    }

    private sealed record CategorySets(IReadOnlyList<string> Amenity, IReadOnlyList<string> Tourism, IReadOnlyList<string> Leisure);
    private sealed record NearbyVenueLookup(Guid Id, string Name, double Latitude, double Longitude);

    private sealed record SearchArea(double? Latitude, double? Longitude, int? RadiusMeters, double? South, double? West, double? North, double? East)
    {
        public static SearchArea Around(double latitude, double longitude, int radiusMeters)
        {
            return new SearchArea(latitude, longitude, radiusMeters, null, null, null, null);
        }

        public static SearchArea BoundingBox(double south, double west, double north, double east)
        {
            return new SearchArea(null, null, null, south, west, north, east);
        }

        public string ToOverpassSelector()
        {
            if (South is not null && West is not null && North is not null && East is not null)
            {
                return string.Create(CultureInfo.InvariantCulture, $"({South},{West},{North},{East})");
            }

            return string.Create(CultureInfo.InvariantCulture, $"(around:{RadiusMeters},{Latitude},{Longitude})");
        }
    }

    private sealed class NominatimResult
    {
        public string? Lat { get; set; }
        public string? Lon { get; set; }
        [JsonPropertyName("boundingbox")]
        public string[]? BoundingBox { get; set; }
    }

    private sealed class OverpassResponse
    {
        public List<OverpassElement> Elements { get; set; } = new();
    }

    private sealed class OverpassElement
    {
        public string Type { get; set; } = string.Empty;
        public long Id { get; set; }
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public OverpassCenter? Center { get; set; }
        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }
    }

    private sealed class OverpassCenter
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
}
