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

public class FoursquareVenueImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;
    private readonly FoursquareOptions _options;

    public FoursquareVenueImportService(
        HttpClient httpClient,
        AppDbContext db,
        IOptions<FoursquareOptions> options)
    {
        _httpClient = httpClient;
        _db = db;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<VenueImportCandidateDto>> PreviewAsync(VenueImportRequest request, CancellationToken ct)
    {
        ValidateConfigured();
        ValidateRequest(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BuildSearchUri(request));
        httpRequest.Headers.TryAddWithoutValidation("Accept", "application/json");
        httpRequest.Headers.TryAddWithoutValidation("Authorization", BuildAuthorizationHeader());
        if (!string.IsNullOrWhiteSpace(_options.ApiVersion))
            httpRequest.Headers.TryAddWithoutValidation("X-Places-Api-Version", _options.ApiVersion);

        using var response = await SendAsync(httpRequest, ct);
        var content = await ReadContentAsync(response, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Foursquare search failed: {(int)response.StatusCode} {response.ReasonPhrase}. {content}");

        var search = JsonSerializer.Deserialize<FoursquareSearchResponse>(content, JsonOptions)
            ?? new FoursquareSearchResponse();

        var candidates = search.Results
            .Select(ToCandidate)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        var providerIds = candidates.Select(x => x.ExternalProviderId).Distinct(StringComparer.Ordinal).ToArray();
        if (providerIds.Length == 0)
            return candidates;

        var existing = await _db.Venues
            .AsNoTracking()
            .Where(x => providerIds.Contains(x.ExternalProviderId))
            .Select(x => new { x.Id, x.ExternalProviderId })
            .ToDictionaryAsync(x => x.ExternalProviderId, x => x.Id, StringComparer.Ordinal, ct);

        return candidates
            .Select(x => existing.TryGetValue(x.ExternalProviderId, out var venueId)
                ? x with { AlreadyExists = true, ExistingVenueId = venueId }
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

    private Uri BuildSearchUri(VenueImportRequest request)
    {
        var query = new Dictionary<string, string?>
        {
            ["ll"] = string.Create(CultureInfo.InvariantCulture, $"{request.Latitude},{request.Longitude}"),
            ["radius"] = request.RadiusMeters.ToString(CultureInfo.InvariantCulture),
            ["limit"] = Math.Clamp(request.Limit, 1, 50).ToString(CultureInfo.InvariantCulture),
            ["fields"] = "fsq_id,name,categories,geocodes,location,tel,website,hours,photos"
        };

        if (!string.IsNullOrWhiteSpace(request.Query))
            query["query"] = request.Query.Trim();

        if (!string.IsNullOrWhiteSpace(request.CategoriesCsv))
            query[UsesNewPlacesApi() ? "categoryId" : "categories"] = NormalizeCsv(request.CategoriesCsv);

        var queryString = string.Join("&", query
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}"));

        var path = string.IsNullOrWhiteSpace(_options.SearchPath)
            ? "places/search"
            : _options.SearchPath.Trim().TrimStart('/');
        return new Uri($"{path}?{queryString}", UriKind.Relative);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Foursquare non ha completato la risposta. Verifica API key, rete e parametri di ricerca. Dettaglio: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException("Timeout durante la chiamata a Foursquare. Riprova con raggio/limite piu piccoli o verifica la rete.", ex);
        }
    }

    private static async Task<string> ReadContentAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            if (!response.IsSuccessStatusCode)
                return $"response body unavailable: {ex.Message}";

            throw new InvalidOperationException($"Foursquare ha chiuso la risposta prima del completamento. Dettaglio: {ex.Message}", ex);
        }
    }

    private static VenueImportCandidateDto? ToCandidate(FoursquarePlace place)
    {
        var latitude = place.Geocodes?.Main?.Latitude;
        var longitude = place.Geocodes?.Main?.Longitude;
        if (string.IsNullOrWhiteSpace(place.FsqId) ||
            string.IsNullOrWhiteSpace(place.Name) ||
            latitude is null ||
            longitude is null)
        {
            return null;
        }

        var category = place.Categories?.FirstOrDefault()?.Name ?? "venue";
        var address = FirstNonEmpty(place.Location?.Address, place.Location?.FormattedAddress, place.Location?.AddressExtended) ?? string.Empty;
        var city = FirstNonEmpty(place.Location?.Locality, place.Location?.PostTown, place.Location?.Region) ?? string.Empty;
        var country = FirstNonEmpty(place.Location?.Country) ?? "IT";
        var tags = place.Categories is null
            ? category
            : string.Join(",", place.Categories.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));

        return new VenueImportCandidateDto(
            $"foursquare:{place.FsqId}",
            place.Name.Trim(),
            category.Trim(),
            address.Trim(),
            city.Trim(),
            country.Trim().ToUpperInvariant(),
            NullIfWhiteSpace(place.Tel),
            NullIfWhiteSpace(place.Website),
            NullIfWhiteSpace(place.Hours?.Display),
            BuildPhotoUrl(place.Photos?.FirstOrDefault()),
            null,
            NullIfWhiteSpace(tags),
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

    private void ValidateConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Foursquare:ApiKey is not configured.");
    }

    private string BuildAuthorizationHeader()
    {
        var apiKey = _options.ApiKey.Trim();
        if (apiKey.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return apiKey;

        return string.Equals(_options.AuthorizationScheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            ? $"Bearer {apiKey}"
            : apiKey;
    }

    private bool UsesNewPlacesApi()
    {
        return _httpClient.BaseAddress?.Host.Contains("places-api.foursquare.com", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void ValidateRequest(VenueImportRequest request)
    {
        if (request.Latitude is < -90 or > 90) throw new ArgumentException("latitude must be between -90 and 90.");
        if (request.Longitude is < -180 or > 180) throw new ArgumentException("longitude must be between -180 and 180.");
        if (request.RadiusMeters is < 1 or > 100000) throw new ArgumentException("radiusMeters must be between 1 and 100000.");
        if (request.Limit is < 1 or > 50) throw new ArgumentException("limit must be between 1 and 50.");
    }

    private static string NormalizeCsv(string value)
    {
        return string.Join(",", value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal));
    }

    private static string? BuildPhotoUrl(FoursquarePhoto? photo)
    {
        if (photo is null || string.IsNullOrWhiteSpace(photo.Prefix) || string.IsNullOrWhiteSpace(photo.Suffix))
            return null;

        return $"{photo.Prefix}original{photo.Suffix}";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class FoursquareSearchResponse
    {
        public List<FoursquarePlace> Results { get; set; } = new();
    }

    private sealed class FoursquarePlace
    {
        [JsonPropertyName("fsq_id")]
        public string? FsqId { get; set; }
        public string? Name { get; set; }
        public FoursquareGeocodes? Geocodes { get; set; }
        public FoursquareLocation? Location { get; set; }
        public List<FoursquareCategory>? Categories { get; set; }
        public string? Tel { get; set; }
        public string? Website { get; set; }
        public FoursquareHours? Hours { get; set; }
        public List<FoursquarePhoto>? Photos { get; set; }
    }

    private sealed class FoursquareGeocodes
    {
        public FoursquareLatLng? Main { get; set; }
    }

    private sealed class FoursquareLatLng
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private sealed class FoursquareLocation
    {
        public string? Address { get; set; }
        [JsonPropertyName("address_extended")]
        public string? AddressExtended { get; set; }
        [JsonPropertyName("formatted_address")]
        public string? FormattedAddress { get; set; }
        public string? Locality { get; set; }
        public string? Region { get; set; }
        public string? Country { get; set; }
        [JsonPropertyName("post_town")]
        public string? PostTown { get; set; }
    }

    private sealed class FoursquareCategory
    {
        public string? Name { get; set; }
    }

    private sealed class FoursquareHours
    {
        public string? Display { get; set; }
    }

    private sealed class FoursquarePhoto
    {
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
    }
}
