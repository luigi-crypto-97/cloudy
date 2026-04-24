using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;
using FriendMap.Mobile.ViewModels;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Maps;

namespace FriendMap.Mobile.Pages;

public partial class MainMapPage
{
    private void RenderMap()
    {
        if (NativeMap?.Handler is null) return;
        NativeMap.Pins.Clear();
        NativeMap.MapElements.Clear();
        BubbleLayer.Children.Clear();
        RenderSelectedPresencePreview();
        _lastOverlayViewport = null;

        var markers = GetRenderableMarkers();
        if (markers.Count == 0)
        {
            EnsureInitialMapRegion();
            return;
        }

        var viewport = GetCurrentViewportOrDefault();
        var allowPins = viewport.LatitudeSpan < 0.085d;
        var allowCircles = viewport.LatitudeSpan < 0.14d;

        RenderAreaPolygons(_viewModel.Areas, viewport);

        foreach (var marker in markers)
        {
            var location = new Location(marker.Latitude, marker.Longitude);

            if (allowPins)
            {
                var pin = new Pin
                {
                    Label = $"{marker.Name} ({marker.PeopleEstimate})",
                    Address = marker.Category,
                    Type = PinType.Place,
                    Location = location
                };
                pin.MarkerClicked += (_, args) =>
                {
                    args.HideInfoWindow = false;
                    HapticService.Light();
                    _viewModel.SelectMarker(marker);
                };
                NativeMap.Pins.Add(pin);
            }

            if (allowCircles)
            {
                NativeMap.MapElements.Add(new Circle
                {
                    Center = location,
                    Radius = Distance.FromMeters(75 + marker.BubbleIntensity * 5),
                    StrokeColor = ResolveSignalColor(marker).WithAlpha(0.55f),
                    StrokeWidth = 2,
                    FillColor = ResolveSignalColor(marker).WithAlpha(0.09f)
                });
            }
        }

        if (_shouldAutoFocusOnNextRender)
        {
            MoveToMarkers(markers, suppressViewportRefresh: true);
            _shouldAutoFocusOnNextRender = false;
        }

        RenderViewportOverlay();
    }

    private List<VenueMarker> GetRenderableMarkers()
    {
        return _viewModel.Markers
            .Where(x => x.Latitude != 0 && x.Longitude != 0)
            .ToList();
    }

    private void MoveToMarkers(IReadOnlyCollection<VenueMarker> markers, bool suppressViewportRefresh)
    {
        if (markers.Count == 0)
        {
            return;
        }

        var centerLat = markers.Average(x => x.Latitude);
        var centerLng = markers.Average(x => x.Longitude);
        var latSpan = Math.Max(0.02, markers.Max(x => x.Latitude) - markers.Min(x => x.Latitude));
        var lngSpan = Math.Max(0.02, markers.Max(x => x.Longitude) - markers.Min(x => x.Longitude));
        var radiusKm = Math.Max(latSpan, lngSpan) * 111;

        if (suppressViewportRefresh)
        {
            SuspendViewportRefreshFor(TimeSpan.FromMilliseconds(900));
        }

        NativeMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(centerLat, centerLng),
            Distance.FromKilometers(Math.Clamp(radiusKm, 2, 20))));
    }

    private void EnsureInitialMapRegion()
    {
        if (NativeMap?.Handler is null || NativeMap.VisibleRegion is not null)
        {
            return;
        }

        SuspendViewportRefreshFor(TimeSpan.FromMilliseconds(900));
        NativeMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(MapViewport.MilanDefault.CenterLatitude, MapViewport.MilanDefault.CenterLongitude),
            Distance.FromKilometers(8)));
    }

    private void RenderAreaPolygons(IReadOnlyList<MapArea> areas, MapViewport viewport)
    {
        if (areas.Count == 0 || viewport.LatitudeSpan > 0.24d)
        {
            return;
        }

        var markerLookup = _viewModel.Markers.ToDictionary(x => x.VenueId);
        foreach (var area in areas.Where(x => x.Polygon.Count >= 3))
        {
            var baseColor = ResolveAreaColor(area);
            var polygon = new Microsoft.Maui.Controls.Maps.Polygon
            {
                FillColor = baseColor.WithAlpha(_isContrastModeEnabled
                    ? (area.IsCluster ? 0.18f : 0.12f)
                    : (area.IsCluster ? 0.11f : 0.07f)),
                StrokeColor = baseColor.WithAlpha(_isContrastModeEnabled
                    ? (area.IsCluster ? 0.58f : 0.42f)
                    : (area.IsCluster ? 0.44f : 0.30f)),
                StrokeWidth = area.IsCluster ? 2.2f : 1.4f
            };

            foreach (var point in area.Polygon)
            {
                polygon.Add(new Location(point.Latitude, point.Longitude));
            }

            NativeMap.MapElements.Add(polygon);

            var coreRadius = Math.Clamp(120 + area.BubbleIntensity * 6, 130, 520);
            NativeMap.MapElements.Add(new Circle
            {
                Center = new Location(area.CentroidLatitude, area.CentroidLongitude),
                Radius = Distance.FromMeters(coreRadius),
                StrokeColor = Colors.Transparent,
                StrokeWidth = 0,
                FillColor = baseColor.WithAlpha(_isContrastModeEnabled ? 0.16f : 0.10f)
            });

            var bloomRadius = Math.Clamp(coreRadius * 1.9, 220, 920);
            NativeMap.MapElements.Add(new Circle
            {
                Center = new Location(area.CentroidLatitude, area.CentroidLongitude),
                Radius = Distance.FromMeters(bloomRadius),
                StrokeColor = Colors.Transparent,
                StrokeWidth = 0,
                FillColor = baseColor.WithAlpha(_isContrastModeEnabled ? 0.08f : 0.05f)
            });

            foreach (var marker in area.VenueIds
                         .Where(markerLookup.ContainsKey)
                         .Select(id => markerLookup[id])
                         .OrderByDescending(x => x.BubbleIntensity)
                         .Take(4))
            {
                var venueRadius = Math.Clamp(70 + marker.BubbleIntensity * 3.4, 82, 260);
                NativeMap.MapElements.Add(new Circle
                {
                    Center = new Location(marker.Latitude, marker.Longitude),
                    Radius = Distance.FromMeters(venueRadius),
                    StrokeColor = Colors.Transparent,
                    StrokeWidth = 0,
                    FillColor = ResolveSignalColor(marker).WithAlpha(_isContrastModeEnabled ? 0.12f : 0.07f)
                });
            }
        }
    }

    private void RenderViewportOverlay()
    {
        BubbleLayer.Children.Clear();
        var viewport = GetCurrentViewportOrDefault().Expand(0.04d).Normalize();
        _lastOverlayViewport = viewport;
        var insets = GetOverlayInsets();

        var markers = GetRenderableMarkers();
        if (markers.Count > 0)
        {
            var visibleMarkers = markers
                .Where(x => viewport.Contains(x.Latitude, x.Longitude))
                .ToList();

            if (UseLightweightVenueBadges)
            {
                if (_selectedAreaClusterKey is not null)
                {
                    ClearAreaSelection();
                }

                RenderVenueCountBadges(visibleMarkers, viewport, insets);
            }
            else if (visibleMarkers.Count > 0 && _viewModel.Areas.Count > 0)
            {
                var clusters = BuildOverlayAreas(visibleMarkers, viewport, insets);
                if (_selectedAreaClusterKey is not null)
                {
                    _activeAreaCluster = clusters.FirstOrDefault(x => x.Key == _selectedAreaClusterKey);
                    if (_activeAreaCluster is null)
                    {
                        ClearAreaSelection();
                    }
                }

                var useNativeCloudAnnotations =
#if IOS
                    EnableNativeIosCloudAnnotations;
#else
                    false;
#endif

                if (useNativeCloudAnnotations)
                {
                    RenderNativeCloudAnnotations(clusters);
                }
                else
                {
                    foreach (var link in BuildFogLinks(clusters))
                    {
                        var ribbon = CreateFogLink(link);
                        AbsoluteLayout.SetLayoutFlags(ribbon, AbsoluteLayoutFlags.None);
                        AbsoluteLayout.SetLayoutBounds(ribbon, link.Bounds);
                        BubbleLayer.Children.Add(ribbon);
                    }

                    foreach (var cluster in clusters)
                    {
                        var bubble = CreateClusterBubble(cluster);
                        AbsoluteLayout.SetLayoutFlags(bubble, AbsoluteLayoutFlags.None);
                        AbsoluteLayout.SetLayoutBounds(bubble, BuildClusterBubbleBounds(cluster));
                        BubbleLayer.Children.Add(bubble);
                    }
                }
            }
            else if (_selectedAreaClusterKey is not null)
            {
                ClearAreaSelection();
            }
        }

        RenderCurrentUserOverlay(viewport, insets);
        RenderAreaSelectionState();
    }

    private void RenderVenueCountBadges(IReadOnlyList<VenueMarker> markers, MapViewport viewport, OverlayInsets insets)
    {
        foreach (var marker in markers)
        {
            if (!TryGetOverlayAnchorPoint(marker.Latitude, marker.Longitude, viewport, insets, out var anchor))
            {
                continue;
            }

            var badge = CreateVenueCountBadge(marker, viewport);
            var size = badge.WidthRequest;
            AbsoluteLayout.SetLayoutFlags(badge, AbsoluteLayoutFlags.None);
            AbsoluteLayout.SetLayoutBounds(badge, new Rect(anchor.X - size / 2d, anchor.Y - size / 2d, size, size));
            BubbleLayer.Children.Add(badge);
        }
    }

    private View CreateVenueCountBadge(VenueMarker marker, MapViewport viewport)
    {
        var peopleCount = GetMarkerPeopleCount(marker);
        var zoomScale = ResolveBubbleZoomScale(viewport);
        var size = Math.Clamp((32 + Math.Min(10, marker.BubbleIntensity / 8d)) * zoomScale, 28, 44);
        var color = ResolveSignalColor(marker);
        var textColor = marker.BubbleIntensity < 25 && marker.ActiveCheckIns == 0 && marker.ActiveIntentions == 0 && marker.OpenTables == 0
            ? Color.FromArgb("#6D28D9")
            : Colors.White;

        var badge = new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            Padding = 0,
            BackgroundColor = color == Colors.Transparent ? Color.FromArgb("#EDE9FE") : color,
            Stroke = Colors.White,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = size / 2d },
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Colors.Black),
                Offset = new Point(0, 4),
                Radius = 9,
                Opacity = 0.16f
            },
            Content = new Label
            {
                Text = peopleCount.ToString(),
                FontSize = peopleCount > 99 ? 10 : 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = textColor,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        badge.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                ClearAreaSelection();
                _viewModel.SelectMarker(marker);
            })
        });

        if (marker.BubbleIntensity >= 55 || marker.OpenTables > 0 || marker.ActiveCheckIns > 0)
        {
            StartVenueBadgePulse(badge, marker.BubbleIntensity);
        }

        return badge;
    }

    private static void StartVenueBadgePulse(VisualElement badge, int intensity)
    {
        badge.AbortAnimation("venue-badge-pulse");
        var targetScale = intensity >= 80 ? 1.08d : 1.05d;
        var targetOpacity = intensity >= 80 ? 1d : 0.94d;
        var animation = new Animation
        {
            { 0, 0.5, new Animation(v => badge.Scale = v, 1d, targetScale, Easing.SinInOut) },
            { 0.5, 1, new Animation(v => badge.Scale = v, targetScale, 1d, Easing.SinInOut) },
            { 0, 0.5, new Animation(v => badge.Opacity = v, 0.96d, targetOpacity, Easing.SinInOut) },
            { 0.5, 1, new Animation(v => badge.Opacity = v, targetOpacity, 0.96d, Easing.SinInOut) }
        };
        animation.Commit(badge, "venue-badge-pulse", 16, 2400, repeat: () => badge.Parent is not null);
    }

    private void ApplyMapMood()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        if (_isContrastModeEnabled)
        {
            MapMoodOverlay.BackgroundColor = isDark
                ? Color.FromArgb("#CC0B0F19")
                : Color.FromArgb("#8A0F172A");
            QuickModeLabel.Text = "Giorno";
            LegendModeLabel.Text = "Modalità notte";
        }
        else
        {
            MapMoodOverlay.BackgroundColor = isDark
                ? Color.FromArgb("#660B0F19")
                : Color.FromArgb("#2CEEF4FF");
            QuickModeLabel.Text = "Notte";
            LegendModeLabel.Text = "Modalità giorno";
        }
    }

    private List<VenueOverlayCluster> BuildOverlayAreas(List<VenueMarker> markers, MapViewport viewport, OverlayInsets insets)
    {
        var markerMap = markers.ToDictionary(x => x.VenueId);
        var zoomScale = ResolveBubbleZoomScale(viewport);

        return _viewModel.Areas
            .Where(x => viewport.Contains(x.CentroidLatitude, x.CentroidLongitude))
            .Select(area =>
            {
                var resolvedMarkers = area.VenueIds
                    .Where(markerMap.ContainsKey)
                    .Select(id => markerMap[id])
                    .ToList();

                if (resolvedMarkers.Count == 0)
                {
                    return null;
                }

                if (!TryGetOverlayAnchorPoint(area.CentroidLatitude, area.CentroidLongitude, viewport, insets, out var anchor))
                {
                    return null;
                }

                var size = Math.Clamp((58 + Math.Min(34, area.BubbleIntensity / 2.9)) * zoomScale * (area.IsCluster ? 1.16d : 1.10d), 54, 132);

                return new VenueOverlayCluster(
                    area.AreaId,
                    resolvedMarkers,
                    area.PresencePreview.ToList(),
                    Math.Max(area.PresenceCount, area.PresencePreview.Count),
                    area.PeopleCount,
                    area.VenueCount,
                    area.Label,
                    anchor.X,
                    anchor.Y,
                    size,
                    zoomScale,
                    ResolveAreaColor(area),
                    area.IsCluster,
                    area.CentroidLatitude,
                    area.CentroidLongitude);
            })
            .Where(x => x is not null)
            .Cast<VenueOverlayCluster>()
            .ToList();
    }

    private List<VenueOverlayCluster> BuildOverlayClusters(List<VenueMarker> markers, MapViewport viewport, OverlayInsets insets)
    {
        var latRange = viewport.LatitudeSpan;
        var lngRange = viewport.LongitudeSpan;
        var usableWidth = Math.Max(0.18d, 1d - insets.Left - insets.Right);
        var usableHeight = Math.Max(0.18d, 1d - insets.Top - insets.Bottom);
        var zoomScale = ResolveBubbleZoomScale(viewport);

        var projected = markers.Select(marker =>
        {
            var normalizedX = (marker.Longitude - viewport.MinLongitude) / lngRange;
            var normalizedY = (marker.Latitude - viewport.MinLatitude) / latRange;
            var x = Math.Clamp(insets.Left + normalizedX * usableWidth, insets.Left + 0.03d, 1d - insets.Right - 0.03d);
            var y = Math.Clamp(1d - insets.Bottom - normalizedY * usableHeight, insets.Top + 0.04d, 1d - insets.Bottom - 0.04d);
            var size = Math.Clamp((30 + Math.Min(16, marker.BubbleIntensity / 4.2)) * zoomScale, 28, 60);
            return new OverlayMarkerProjection(marker, x, y, size);
        }).ToList();

        var clusterCellSize = ResolveClusterCellSize(viewport);
        if (clusterCellSize <= 0)
        {
            return projected
                .Select(x => CreateSingleMarkerCluster(x.Marker, x.X, x.Y, x.Size))
                .ToList();
        }

        return projected
            .GroupBy(x => (
                Col: (int)Math.Floor((x.X - insets.Left) / clusterCellSize),
                Row: (int)Math.Floor((x.Y - insets.Top) / clusterCellSize)))
            .Select(group =>
            {
                if (group.Count() == 1)
                {
                    var single = group.First();
                    return CreateSingleMarkerCluster(single.Marker, single.X, single.Y, single.Size);
                }

                var groupedMarkers = group.Select(x => x.Marker).ToList();
                var uniquePresence = groupedMarkers
                    .SelectMany(x => x.PresencePreview)
                    .GroupBy(x => x.UserId)
                    .Select(x => x.First())
                    .ToList();

                var peopleCount = groupedMarkers.Sum(GetMarkerPeopleCount);
                var areaLabel = BuildAreaLabel(groupedMarkers);
                var key = BuildClusterKey(groupedMarkers);

                return new VenueOverlayCluster(
                    key,
                    groupedMarkers,
                    uniquePresence.Take(4).ToList(),
                    uniquePresence.Count,
                    peopleCount,
                    groupedMarkers.Count,
                    areaLabel,
                    group.Average(x => x.X),
                    group.Average(x => x.Y),
                    Math.Clamp(group.Max(x => x.Size) + 12, 48, 104),
                    zoomScale,
                    ResolveClusterColor(groupedMarkers),
                    true,
                    groupedMarkers.Average(x => x.Latitude),
                    groupedMarkers.Average(x => x.Longitude));
            })
            .ToList();
    }

    private static double ResolveClusterCellSize(MapViewport viewport)
    {
        return viewport.LatitudeSpan switch
        {
            < 0.020d => 0d,
            < 0.045d => 0.095d,
            < 0.085d => 0.13d,
            _ => 0.17d
        };
    }

    private static double ResolveBubbleZoomScale(MapViewport viewport)
    {
        var normalized = 0.035d / Math.Max(0.006d, viewport.LatitudeSpan);
        return Math.Clamp(Math.Pow(normalized, 0.25d), 0.36d, 1.20d);
    }

    private List<FogLink> BuildFogLinks(IReadOnlyList<VenueOverlayCluster> clusters)
    {
        var links = new List<FogLink>();
        for (var i = 0; i < clusters.Count; i++)
        {
            for (var j = i + 1; j < clusters.Count; j++)
            {
                var first = clusters[i];
                var second = clusters[j];
                var dx = second.X - first.X;
                var dy = second.Y - first.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                var maxDistance = Math.Max(110d, (first.Size + second.Size) * 1.45d);
                if (distance < 24d || distance > maxDistance)
                {
                    continue;
                }

                var width = distance + Math.Max(first.Size, second.Size) * 0.95d;
                var height = Math.Max(30d, (first.Size + second.Size) * 0.42d);
                var centerX = (first.X + second.X) / 2d;
                var centerY = (first.Y + second.Y) / 2d - 6d;
                var angle = Math.Atan2(dy, dx) * 180d / Math.PI;
                var intensity = Math.Clamp((first.PeopleCount + second.PeopleCount) / 18d, 0.42d, 1d);
                var color = first.PeopleCount >= second.PeopleCount ? first.Color : second.Color;
                var bodyAlpha = _isContrastModeEnabled ? 0.24f : 0.18f;
                var glowAlpha = _isContrastModeEnabled ? 0.16f : 0.10f;
                links.Add(new FogLink(
                    new Rect(centerX - width / 2d, centerY - height / 2d, width, height),
                    angle,
                    height,
                    color.WithAlpha(bodyAlpha),
                    color.WithAlpha(glowAlpha),
                    intensity));
            }
        }

        return links
            .OrderByDescending(x => x.Intensity)
            .Take(18)
            .ToList();
    }

    private View CreateClusterBubble(VenueOverlayCluster cluster)
    {
        var isSelectedArea = _selectedAreaClusterKey == cluster.Key;
        var shadowOpacity = isSelectedArea ? 0.16f : 0.08f;
        var contrastBoost = _isContrastModeEnabled ? 1.18d : 1d;
        var cloud = new AbsoluteLayout
        {
            WidthRequest = cluster.LayoutWidth,
            HeightRequest = cluster.LayoutHeight,
            InputTransparent = false,
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Colors.Black),
                Radius = isSelectedArea ? 26 : 20,
                Offset = new Point(0, 10),
                Opacity = shadowOpacity
            }
        };

        var farField = new Border
        {
            WidthRequest = cluster.Size * (2.35 * contrastBoost),
            HeightRequest = cluster.Size * (1.66 * contrastBoost),
            Background = CreateFogFieldBrush(cluster.Color, isSelectedArea),
            StrokeThickness = 0,
            Opacity = isSelectedArea ? (_isContrastModeEnabled ? 0.94 : 0.86) : (_isContrastModeEnabled ? 0.80 : 0.72),
            StrokeShape = new RoundRectangle { CornerRadius = cluster.Size * 0.90 }
        };
        AbsoluteLayout.SetLayoutBounds(farField, new Rect(cluster.LayoutWidth * -0.18, cluster.CloudYOffset - cluster.Size * 0.06, cluster.Size * (2.35 * contrastBoost), cluster.Size * (1.66 * contrastBoost)));
        cloud.Children.Add(farField);

        var outerAura = new Border
        {
            WidthRequest = cluster.Size * 2.02,
            HeightRequest = cluster.Size * 1.28,
            Background = CreateCloudHaloBrush(cluster.Color),
            StrokeThickness = 0,
            Opacity = isSelectedArea ? (_isContrastModeEnabled ? 0.94 : 0.88) : (_isContrastModeEnabled ? 0.82 : 0.76),
            StrokeShape = new RoundRectangle { CornerRadius = cluster.Size * 0.72 }
        };
        AbsoluteLayout.SetLayoutBounds(outerAura, new Rect(cluster.LayoutWidth * -0.04, cluster.CloudYOffset + cluster.Size * 0.04, cluster.Size * 2.02, cluster.Size * 1.28));
        cloud.Children.Add(outerAura);

        var aura = new Border
        {
            WidthRequest = cluster.Size * 1.46,
            HeightRequest = cluster.Size * 0.96,
            Background = CreateCloudAuraBrush(cluster.Color, isSelectedArea),
            StrokeThickness = 0,
            Opacity = isSelectedArea ? (_isContrastModeEnabled ? 0.97 : 0.94) : (_isContrastModeEnabled ? 0.90 : 0.86),
            StrokeShape = new RoundRectangle { CornerRadius = cluster.Size * 0.56 }
        };
        AbsoluteLayout.SetLayoutBounds(aura, new Rect(cluster.LayoutWidth * 0.08, cluster.CloudYOffset + cluster.Size * 0.18, cluster.Size * 1.46, cluster.Size * 0.96));
        cloud.Children.Add(aura);

        var fill = CreateCloudBrush(cluster.Color, isSelectedArea);
        AddCloudPuff(cloud, cluster.Size * 1.08, cluster.Size * 0.64, cluster.LayoutWidth * 0.04, cluster.CloudYOffset + cluster.Size * 0.34, fill, Colors.Transparent, isSelectedArea);
        AddCloudPuff(cloud, cluster.Size * 1.22, cluster.Size * 0.88, cluster.LayoutWidth * 0.16, cluster.CloudYOffset + cluster.Size * 0.04, fill, Colors.Transparent, isSelectedArea);
        AddCloudPuff(cloud, cluster.Size * 1.08, cluster.Size * 0.72, cluster.LayoutWidth * 0.54, cluster.CloudYOffset + cluster.Size * 0.22, fill, Colors.Transparent, isSelectedArea);
        AddCloudPuff(cloud, cluster.Size * 0.84, cluster.Size * 0.52, cluster.LayoutWidth * 0.34, cluster.CloudYOffset + cluster.Size * 0.46, fill, Colors.Transparent, isSelectedArea);
        AddCloudPuff(cloud, cluster.Size * 0.74, cluster.Size * 0.44, cluster.LayoutWidth * 0.78, cluster.CloudYOffset + cluster.Size * 0.42, fill, Colors.Transparent, isSelectedArea);
        AddCloudHighlight(cloud, cluster.Size * 0.30, cluster.Size * 0.11, cluster.LayoutWidth * 0.34, cluster.CloudYOffset + cluster.Size * 0.16);
        AddCloudHighlight(cloud, cluster.Size * 0.26, cluster.Size * 0.10, cluster.LayoutWidth * 0.68, cluster.CloudYOffset + cluster.Size * 0.26);

        if (viewportOrDefaultForCount(cluster))
        {
            var countPill = new Border
            {
                Padding = new Thickness(8, 3),
                BackgroundColor = Colors.White.WithAlpha(isSelectedArea ? 0.92f : 0.82f),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = new Label
                {
                    Text = cluster.PeopleCount.ToString(),
                    TextColor = Color.FromArgb("#0F172A"),
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 11,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };
            var pillWidth = Math.Max(30d, 18d + cluster.PeopleCount.ToString().Length * 7d);
            AbsoluteLayout.SetLayoutBounds(countPill, new Rect((cluster.LayoutWidth - pillWidth) / 2d, cluster.CloudYOffset + cluster.Size * 0.74, pillWidth, 24));
            cloud.Children.Add(countPill);
        }

        cloud.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnClusterTappedAsync(cluster))
        });

        StartCloudPulse(cloud, cluster.BubbleScaleHint);

        return cloud;
    }

    private View CreateFogLink(FogLink link)
    {
        var host = new Grid
        {
            WidthRequest = link.Bounds.Width,
            HeightRequest = link.Bounds.Height,
            Rotation = link.Angle,
            InputTransparent = true,
            Opacity = link.Intensity
        };

        var bloom = new Border
        {
            WidthRequest = link.Bounds.Width * 1.05,
            HeightRequest = link.Bounds.Height * 1.55,
            Background = new RadialGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(link.GlowColor, 0f),
                    new GradientStop(link.GlowColor.WithAlpha(link.GlowColor.Alpha * 0.55f), 0.48f),
                    new GradientStop(link.GlowColor.WithAlpha(0f), 1f)
                },
                new Point(0.5, 0.5),
                1f),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = link.Height }
        };

        var haze = new Border
        {
            WidthRequest = link.Bounds.Width,
            HeightRequest = link.Bounds.Height,
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(link.Color.WithAlpha(0f), 0f),
                    new GradientStop(link.Color, 0.30f),
                    new GradientStop(link.Color.WithAlpha(Math.Min(0.30f, link.Color.Alpha)), 0.50f),
                    new GradientStop(link.Color, 0.70f),
                    new GradientStop(link.Color.WithAlpha(0f), 1f)
                },
                new Point(0, 0.5),
                new Point(1, 0.5)),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = link.Height / 2d }
        };

        host.Children.Add(bloom);
        host.Children.Add(haze);
        return host;
    }

    private async Task OnClusterTappedAsync(VenueOverlayCluster cluster)
    {
        await HidePresenceOverlayAsync(animated: false);
        await HideProfileOverlayAsync(animated: false);
        await HideVenueDetailOverlayAsync(animated: false);

        if (!cluster.IsCluster || cluster.Markers.Count == 1)
        {
            ClearAreaSelection();
            _viewModel.SelectMarker(cluster.Markers[0]);
            return;
        }

        if (_selectedAreaClusterKey == cluster.Key)
        {
            await ShowPresenceOverlayAsync(cluster);
            return;
        }

        _viewModel.ClearSelection();
        _activeAreaCluster = cluster;
        _selectedAreaClusterKey = cluster.Key;
        RenderAreaSelectionState();
        RenderViewportOverlay();
    }

}
