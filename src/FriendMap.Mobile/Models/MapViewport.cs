namespace FriendMap.Mobile.Models;

public readonly record struct MapViewport(
    double MinLatitude,
    double MinLongitude,
    double MaxLatitude,
    double MaxLongitude)
{
    public static MapViewport MilanDefault => new(45.4200, 9.1300, 45.5000, 9.2600);

    public double CenterLatitude => (MinLatitude + MaxLatitude) / 2d;
    public double CenterLongitude => (MinLongitude + MaxLongitude) / 2d;
    public double LatitudeSpan => Math.Max(0.0001d, MaxLatitude - MinLatitude);
    public double LongitudeSpan => Math.Max(0.0001d, MaxLongitude - MinLongitude);

    public MapViewport Normalize()
    {
        var south = Math.Min(MinLatitude, MaxLatitude);
        var north = Math.Max(MinLatitude, MaxLatitude);
        var west = Math.Min(MinLongitude, MaxLongitude);
        var east = Math.Max(MinLongitude, MaxLongitude);
        return new MapViewport(south, west, north, east);
    }

    public MapViewport Expand(double factor)
    {
        var normalized = Normalize();
        var latPadding = normalized.LatitudeSpan * factor;
        var lngPadding = normalized.LongitudeSpan * factor;

        return new MapViewport(
            normalized.MinLatitude - latPadding,
            normalized.MinLongitude - lngPadding,
            normalized.MaxLatitude + latPadding,
            normalized.MaxLongitude + lngPadding);
    }

    public bool Contains(double latitude, double longitude)
    {
        var normalized = Normalize();
        return latitude >= normalized.MinLatitude &&
               latitude <= normalized.MaxLatitude &&
               longitude >= normalized.MinLongitude &&
               longitude <= normalized.MaxLongitude;
    }

    public bool IsMeaningfullyDifferentFrom(MapViewport other)
    {
        var current = Normalize();
        var previous = other.Normalize();

        var centerLatDelta = Math.Abs(current.CenterLatitude - previous.CenterLatitude);
        var centerLngDelta = Math.Abs(current.CenterLongitude - previous.CenterLongitude);
        var latSpanDelta = Math.Abs(current.LatitudeSpan - previous.LatitudeSpan);
        var lngSpanDelta = Math.Abs(current.LongitudeSpan - previous.LongitudeSpan);

        return centerLatDelta > previous.LatitudeSpan * 0.18d ||
               centerLngDelta > previous.LongitudeSpan * 0.18d ||
               latSpanDelta > previous.LatitudeSpan * 0.22d ||
               lngSpanDelta > previous.LongitudeSpan * 0.22d;
    }
}
