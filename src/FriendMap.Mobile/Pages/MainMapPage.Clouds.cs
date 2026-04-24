using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace FriendMap.Mobile.Pages;

public partial class MainMapPage
{
    private void RenderAreaSelectionState()
    {
        if (_activeAreaCluster is null || !_activeAreaCluster.IsCluster)
        {
            AreaSelectionCard.IsVisible = false;
            return;
        }

        AreaSelectionTitleLabel.Text = _activeAreaCluster.AreaLabel;
        AreaSelectionMetaLabel.Text = $"{_activeAreaCluster.PeopleCount} persone • {_activeAreaCluster.VenueCount} luoghi";
        AreaSelectionCard.IsVisible = true;
    }

    private void ClearAreaSelection()
    {
        _activeAreaCluster = null;
        _selectedAreaClusterKey = null;
        AreaSelectionCard.IsVisible = false;
    }

    private static int GetMarkerPeopleCount(VenueMarker marker)
    {
        return Math.Max(marker.PeopleEstimate, marker.ActiveCheckIns + marker.ActiveIntentions);
    }

    private static string BuildClusterKey(IEnumerable<VenueMarker> markers)
    {
        return string.Join('|', markers.Select(x => x.VenueId.ToString("N")).OrderBy(x => x, StringComparer.Ordinal));
    }

    private static string BuildAreaLabel(IEnumerable<VenueMarker> markers)
    {
        var lead = markers
            .OrderByDescending(x => GetMarkerPeopleCount(x))
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

    private static Brush CreateCloudBrush(Color signalColor, bool isSelectedArea)
    {
        var topColor = Colors.White.WithAlpha(isSelectedArea ? 0.62f : 0.48f);
        var middleColor = signalColor.WithAlpha(isSelectedArea ? 0.24f : 0.18f);
        var bottomColor = signalColor.WithAlpha(isSelectedArea ? 0.30f : 0.22f);
        return new RadialGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(topColor, 0f),
                new GradientStop(middleColor, 0.52f),
                new GradientStop(bottomColor, 1f)
            },
            new Point(0.46, 0.34),
            0.92f);
    }

    private static Brush CreateFogFieldBrush(Color signalColor, bool isSelectedArea)
    {
        return new RadialGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(signalColor.WithAlpha(isSelectedArea ? 0.24f : 0.18f), 0f),
                new GradientStop(signalColor.WithAlpha(isSelectedArea ? 0.16f : 0.11f), 0.34f),
                new GradientStop(signalColor.WithAlpha(isSelectedArea ? 0.09f : 0.06f), 0.68f),
                new GradientStop(signalColor.WithAlpha(0f), 1f)
            },
            new Point(0.5, 0.5),
            1.05f);
    }

    private static Brush CreateCloudAuraBrush(Color signalColor, bool isSelectedArea)
    {
        return new RadialGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(signalColor.WithAlpha(isSelectedArea ? 0.22f : 0.16f), 0f),
                new GradientStop(signalColor.WithAlpha(isSelectedArea ? 0.14f : 0.09f), 0.50f),
                new GradientStop(signalColor.WithAlpha(0.02f), 1f)
            },
            new Point(0.5, 0.5),
            0.92f);
    }

    private static Brush CreateCloudHaloBrush(Color signalColor)
    {
        return new RadialGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(signalColor.WithAlpha(0.18f), 0f),
                new GradientStop(signalColor.WithAlpha(0.10f), 0.42f),
                new GradientStop(signalColor.WithAlpha(0.04f), 0.74f),
                new GradientStop(signalColor.WithAlpha(0.00f), 1f)
            },
            new Point(0.5, 0.5),
            1f);
    }

    private static void AddCloudPuff(AbsoluteLayout host, double width, double height, double x, double y, Brush fill, Color stroke, bool isSelectedArea)
    {
        var puff = new Border
        {
            WidthRequest = width,
            HeightRequest = height,
            Background = fill,
            Stroke = stroke,
            StrokeThickness = stroke == Colors.Transparent ? 0 : (isSelectedArea ? 1.2 : 0.6),
            StrokeShape = new RoundRectangle { CornerRadius = Math.Min(width, height) / 2d }
        };

        AbsoluteLayout.SetLayoutBounds(puff, new Rect(x, y, width, height));
        host.Children.Add(puff);
    }

    private static void AddCloudHighlight(AbsoluteLayout host, double width, double height, double x, double y)
    {
        var highlight = new Border
        {
            WidthRequest = width,
            HeightRequest = height,
            BackgroundColor = Colors.White.WithAlpha(0.56f),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = Math.Min(width, height) / 2d }
        };

        AbsoluteLayout.SetLayoutBounds(highlight, new Rect(x, y, width, height));
        host.Children.Add(highlight);
    }

    private void StartCloudPulse(VisualElement cloud, double pulseHint)
    {
        cloud.AbortAnimation("cloud-pulse");
        var targetScale = 1 + Math.Min(0.085, pulseHint * 0.032);
        var targetOpacity = 0.95 - Math.Min(0.12, pulseHint * 0.04);
        var animation = new Animation
        {
            { 0, 0.5, new Animation(v => cloud.Scale = v, 0.99, targetScale, Easing.SinInOut) },
            { 0.5, 1, new Animation(v => cloud.Scale = v, targetScale, 0.99, Easing.SinInOut) },
            { 0, 0.5, new Animation(v => cloud.Opacity = v, 0.94, targetOpacity, Easing.SinInOut) },
            { 0.5, 1, new Animation(v => cloud.Opacity = v, targetOpacity, 0.94, Easing.SinInOut) }
        };
        animation.Commit(cloud, "cloud-pulse", 16, 3100, repeat: () => cloud.Parent is not null);
    }

    private bool viewportOrDefaultForCount(VenueOverlayCluster cluster)
    {
        var viewport = _lastOverlayViewport ?? GetCurrentViewportOrDefault();
        return _selectedAreaClusterKey == cluster.Key || !cluster.IsCluster || viewport.LatitudeSpan < 0.030d;
    }
}
