using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;
using FriendMap.Mobile.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Maps;

namespace FriendMap.Mobile.Pages;

public partial class MainMapPage
{
    private async Task SyncVenueSheetAsync(bool animated)
    {
        if (_viewModel.HasSelectedMarker)
        {
            VenueSheet.IsVisible = true;
            var target = VenueSheet.TranslationY >= GetHiddenSheetOffset() - 1 || VenueSheet.TranslationY <= 0
                ? GetOffsetForSheetState(_sheetSnapState)
                : ResolveNearestVisibleSheetOffset(VenueSheet.TranslationY);

            if (VenueSheet.TranslationY >= GetHiddenSheetOffset() - 1)
            {
                _sheetSnapState = VenueSheetSnapState.Teaser;
                target = GetTeaserSheetOffset();
            }

            await AnimateVenueSheetToAsync(target, animated);
            await ShowBumpFabAsync();
            return;
        }

        if (!VenueSheet.IsVisible)
        {
            VenueSheet.TranslationY = GetHiddenSheetOffset();
            await HideBumpFabAsync();
            return;
        }

        await AnimateVenueSheetToAsync(GetHiddenSheetOffset(), animated);
        VenueSheet.IsVisible = false;
        _sheetSnapState = VenueSheetSnapState.Teaser;
        await HideBumpFabAsync();
    }

    private async Task AnimateVenueSheetToAsync(double target, bool animated)
    {
        target = ClampSheetOffset(target);
        if (!animated)
        {
            VenueSheet.TranslationY = target;
            return;
        }

        await VenueSheet.TranslateTo(0, target, 280, Easing.SpringOut);
    }

    private async Task ShowBumpFabAsync()
    {
        if (BumpFab.IsVisible) return;
        BumpFab.IsVisible = true;
        BumpFab.Opacity = 0;
        BumpFab.Scale = 0.8;
        await Task.WhenAll(
            BumpFab.FadeTo(1, 220, Easing.CubicOut),
            BumpFab.ScaleTo(1, 320, Easing.SpringOut)
        );
    }

    private async Task HideBumpFabAsync()
    {
        if (!BumpFab.IsVisible) return;
        await Task.WhenAll(
            BumpFab.FadeTo(0, 180, Easing.CubicIn),
            BumpFab.ScaleTo(0.8, 180, Easing.CubicIn)
        );
        BumpFab.IsVisible = false;
    }

    private double ResolveNearestSheetOffset(double currentOffset)
    {
        var offsets = new[]
        {
            GetExpandedSheetOffset(),
            GetCollapsedSheetOffset(),
            GetTeaserSheetOffset(),
            GetHiddenSheetOffset()
        };

        return offsets.OrderBy(x => Math.Abs(x - currentOffset)).First();
    }

    private double ResolveNearestVisibleSheetOffset(double currentOffset)
    {
        var offsets = new[]
        {
            GetExpandedSheetOffset(),
            GetCollapsedSheetOffset(),
            GetTeaserSheetOffset()
        };

        return offsets.OrderBy(x => Math.Abs(x - currentOffset)).First();
    }

    private void UpdateSheetSnapState(double offset)
    {
        if (Math.Abs(offset - GetExpandedSheetOffset()) < 4)
        {
            _sheetSnapState = VenueSheetSnapState.Expanded;
            return;
        }

        if (Math.Abs(offset - GetCollapsedSheetOffset()) < 4)
        {
            _sheetSnapState = VenueSheetSnapState.Collapsed;
            return;
        }

        _sheetSnapState = VenueSheetSnapState.Teaser;
    }

    private double GetOffsetForSheetState(VenueSheetSnapState snapState)
    {
        return snapState switch
        {
            VenueSheetSnapState.Expanded => GetExpandedSheetOffset(),
            VenueSheetSnapState.Collapsed => GetCollapsedSheetOffset(),
            _ => GetTeaserSheetOffset()
        };
    }

    private static double GetExpandedSheetOffset()
    {
        return 0d;
    }

    private double GetTeaserSheetOffset()
    {
        var hidden = GetHiddenSheetOffset();
        return Math.Clamp(hidden - MinimumTeaserVisibleHeight, 108, hidden - 24);
    }

    private double GetCollapsedSheetOffset()
    {
        var hidden = GetHiddenSheetOffset();
        return Math.Clamp(hidden - Math.Min(Math.Max(MinimumCollapsedVisibleHeight, VenueSheet.Height * 0.60d), VenueSheet.Height - 32), 72, hidden - 72);
    }

    private double ClampSheetOffset(double value)
    {
        return Math.Clamp(value, GetExpandedSheetOffset(), GetHiddenSheetOffset());
    }

    private double GetHiddenSheetOffset()
    {
        return Math.Max(320, VenueSheet.Height + HiddenSheetPadding);
    }

    private MapViewport GetCurrentViewportOrDefault()
    {
        if (NativeMap?.VisibleRegion is not MapSpan visibleRegion)
        {
            return MapViewport.MilanDefault;
        }

        var halfLat = Math.Abs(visibleRegion.LatitudeDegrees) / 2d;
        var halfLng = Math.Abs(visibleRegion.LongitudeDegrees) / 2d;
        return new MapViewport(
            visibleRegion.Center.Latitude - halfLat,
            visibleRegion.Center.Longitude - halfLng,
            visibleRegion.Center.Latitude + halfLat,
            visibleRegion.Center.Longitude + halfLng).Normalize();
    }

    private OverlayInsets GetOverlayInsets()
    {
        var pageHeight = Height <= 0 ? 844d : Height;
        var pageWidth = Width <= 0 ? 390d : Width;
        var topChromeHeight = DiscoveryChromePanel.IsVisible ? 132d : 72d;
        var topInset = Math.Clamp(topChromeHeight / pageHeight, 0.08d, 0.22d);

        var sheetVisibleHeight = VenueSheet.IsVisible
            ? Math.Max(0d, VenueSheet.Height - VenueSheet.TranslationY)
            : 0d;
        var bottomChrome = Math.Max(126d, 92d + sheetVisibleHeight);
        if (AreaSelectionCard.IsVisible)
        {
            bottomChrome = Math.Max(bottomChrome, 216d);
        }
        if (PresenceOverlay.IsVisible)
        {
            bottomChrome = Math.Max(bottomChrome, Math.Max(250d, PresenceSheet.Height * 0.82d + 88d));
        }
        if (ProfileOverlay.IsVisible)
        {
            bottomChrome = Math.Max(bottomChrome, Math.Max(320d, ProfileSheet.Height * 0.86d + 78d));
        }
        if (VenueDetailOverlay.IsVisible)
        {
            bottomChrome = Math.Max(bottomChrome, Math.Max(380d, VenueDetailSheet.Height * 0.90d + 72d));
        }
        if (SocialOverlay.IsVisible)
        {
            bottomChrome = Math.Max(bottomChrome, Math.Max(340d, SocialSheet.Height * 0.88d + 72d));
        }
        if (TableOverlay.IsVisible)
        {
            bottomChrome = Math.Max(bottomChrome, Math.Max(360d, TableSheet.Height * 0.88d + 72d));
        }
        if (EditProfileOverlay.IsVisible)
        {
            bottomChrome = Math.Max(bottomChrome, Math.Max(360d, EditProfileSheet.Height * 0.88d + 72d));
        }
        if (DirectMessageOverlay.IsVisible)
        {
            bottomChrome = Math.Max(bottomChrome, Math.Max(360d, DirectMessageSheet.Height * 0.88d + 72d));
        }

        var bottomInset = Math.Clamp(bottomChrome / pageHeight, 0.16d, 0.62d);
        var leftInset = Math.Clamp(24d / pageWidth, 0.05d, 0.10d);
        var rightInset = Math.Clamp((_isQuickActionRailOpen ? 164d : 24d) / pageWidth, 0.05d, 0.34d);
        return new OverlayInsets(leftInset, topInset, rightInset, bottomInset);
    }

    private void StartOverlaySyncTimer()
    {
        if (_overlaySyncTimer is not null || Dispatcher is null)
        {
            return;
        }

        _overlaySyncTimer = Dispatcher.CreateTimer();
        _overlaySyncTimer.Interval = TimeSpan.FromMilliseconds(OverlaySyncIntervalMs);
        _overlaySyncTimer.Tick += (_, _) =>
        {
            if (NativeMap.VisibleRegion is null)
            {
                return;
            }

            var viewport = GetCurrentViewportOrDefault();
            if (_lastOverlayViewport is MapViewport previous &&
                !HasOverlayViewportChanged(viewport, previous))
            {
                return;
            }

            ScheduleOverlayRender(TimeSpan.Zero);
        };
        _overlaySyncTimer.Start();
    }

    private void StopOverlaySyncTimer()
    {
        if (_overlaySyncTimer is null)
        {
            return;
        }

        _overlaySyncTimer.Stop();
        _overlaySyncTimer = null;
    }

    private void ScheduleViewportRefresh()
    {
        CancelPendingViewportRefresh();
        _viewportRefreshCts = new CancellationTokenSource();
        var token = _viewportRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ViewportRefreshDelayMs, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await RefreshForCurrentViewportAsync(force: false, centerOnMarkers: false);
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private void CancelPendingViewportRefresh()
    {
        if (_viewportRefreshCts is null)
        {
            return;
        }

        _viewportRefreshCts.Cancel();
        _viewportRefreshCts.Dispose();
        _viewportRefreshCts = null;
    }

    private void ScheduleOverlayRender(TimeSpan delay)
    {
        CancelPendingOverlayRender();
        _overlayRenderCts = new CancellationTokenSource();
        var token = _overlayRenderCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, token);
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(RenderViewportOverlay);
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private void CancelPendingOverlayRender()
    {
        if (_overlayRenderCts is null)
        {
            return;
        }

        _overlayRenderCts.Cancel();
        _overlayRenderCts.Dispose();
        _overlayRenderCts = null;
    }

    private void SuspendViewportRefreshFor(TimeSpan duration)
    {
        _suspendViewportRefreshUntilUtc = DateTimeOffset.UtcNow.Add(duration);
    }

    private static bool HasOverlayViewportChanged(MapViewport current, MapViewport previous)
    {
        var centerLatDelta = Math.Abs(current.CenterLatitude - previous.CenterLatitude);
        var centerLngDelta = Math.Abs(current.CenterLongitude - previous.CenterLongitude);
        var latSpanDelta = Math.Abs(current.LatitudeSpan - previous.LatitudeSpan);
        var lngSpanDelta = Math.Abs(current.LongitudeSpan - previous.LongitudeSpan);

        return centerLatDelta > previous.LatitudeSpan * 0.012d ||
               centerLngDelta > previous.LongitudeSpan * 0.012d ||
               latSpanDelta > previous.LatitudeSpan * 0.02d ||
               lngSpanDelta > previous.LongitudeSpan * 0.02d;
    }

    private bool TryGetOverlayAnchorPoint(double latitude, double longitude, MapViewport viewport, OverlayInsets insets, out Point anchor)
    {
#if IOS
        if (TryProjectCoordinateToScreenIos(latitude, longitude, out anchor))
        {
            var width = GetOverlayLayerWidth();
            var height = GetOverlayLayerHeight();
            const double margin = 64d;
            return anchor.X >= -margin &&
                   anchor.Y >= -margin &&
                   anchor.X <= width + margin &&
                   anchor.Y <= height + margin;
        }
#endif
        anchor = ProjectCoordinateFallback(latitude, longitude, viewport, insets);
        return true;
    }

    private Point ProjectCoordinateFallback(double latitude, double longitude, MapViewport viewport, OverlayInsets insets)
    {
        var latRange = viewport.LatitudeSpan;
        var lngRange = viewport.LongitudeSpan;
        var usableWidth = Math.Max(0.18d, 1d - insets.Left - insets.Right);
        var usableHeight = Math.Max(0.18d, 1d - insets.Top - insets.Bottom);
        var normalizedX = (longitude - viewport.MinLongitude) / lngRange;
        var normalizedY = (latitude - viewport.MinLatitude) / latRange;
        var proportionalX = Math.Clamp(insets.Left + normalizedX * usableWidth, 0d, 1d);
        var proportionalY = Math.Clamp(1d - insets.Bottom - normalizedY * usableHeight, 0d, 1d);
        return new Point(proportionalX * GetOverlayLayerWidth(), proportionalY * GetOverlayLayerHeight());
    }

    private double GetOverlayLayerWidth()
    {
        return BubbleLayer.Width > 1 ? BubbleLayer.Width : Math.Max(Width, 390d);
    }

    private double GetOverlayLayerHeight()
    {
        return BubbleLayer.Height > 1 ? BubbleLayer.Height : Math.Max(Height, 844d);
    }

    private static Rect BuildClusterBubbleBounds(VenueOverlayCluster cluster)
    {
        var x = cluster.X - cluster.LayoutWidth / 2d;
        var y = cluster.Y - cluster.AnchorYOffset;
        return new Rect(x, y, cluster.LayoutWidth, cluster.LayoutHeight);
    }

    private async Task WarmPersonalMapContextAsync()
    {
        try
        {
            if (_myProfile is null)
            {
                _myProfile = await _apiClient.GetMyProfileAsync();
            }
        }
        catch
        {
            // Personal context is best-effort; the map still works without it.
        }

        await RefreshCurrentUserLocationAsync(showErrors: false, centerMap: false, force: true);
    }

    private async Task RefreshCurrentUserLocationAsync(bool showErrors, bool centerMap, bool force)
    {
        if (_isRefreshingCurrentUserLocation)
        {
            return;
        }

        if (!force &&
            _currentUserLocation is not null &&
            DateTimeOffset.UtcNow - _lastCurrentUserLocationRefreshUtc < CurrentUserLocationCacheDuration)
        {
            if (centerMap)
            {
                MoveMapToUserLocation(_currentUserLocation, "Mappa centrata sulla tua posizione.");
            }

            return;
        }

        try
        {
            _isRefreshingCurrentUserLocation = true;
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
                if (!showErrors)
                {
                    return;
                }

                permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (permission != PermissionStatus.Granted)
            {
                if (showErrors)
                {
                    SetMapStatus("Permesso posizione non concesso.", true);
                }

                return;
            }

            var location = await Geolocation.Default.GetLastKnownLocationAsync();
            location ??= await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8)));

            if (location is null)
            {
                if (showErrors)
                {
                    SetMapStatus("Posizione non disponibile sul dispositivo.", true);
                }

                return;
            }

            _currentUserLocation = new Location(location.Latitude, location.Longitude);
            _lastCurrentUserLocationRefreshUtc = DateTimeOffset.UtcNow;
            _lastOverlayViewport = null;

            if (centerMap)
            {
                MoveMapToUserLocation(_currentUserLocation, "Mappa centrata sulla tua posizione.");
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(RenderViewportOverlay);
            }
        }
        catch (FeatureNotSupportedException)
        {
            if (showErrors)
            {
                SetMapStatus("Geolocalizzazione non supportata su questo device.", true);
            }
        }
        catch (Exception ex)
        {
            if (showErrors)
            {
                SetMapStatus(_apiClient.DescribeException(ex), true);
            }
        }
        finally
        {
            _isRefreshingCurrentUserLocation = false;
        }
    }

    private void MoveMapToUserLocation(Location location, string statusMessage)
    {
        SuspendViewportRefreshFor(TimeSpan.FromMilliseconds(1200));
        _lastOverlayViewport = null;
        NativeMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(1.2)));
        SetMapStatus(statusMessage, false);
        MainThread.BeginInvokeOnMainThread(RenderViewportOverlay);
    }

    private void RenderCurrentUserOverlay(MapViewport viewport, OverlayInsets insets)
    {
        if (_currentUserLocation is null)
        {
            return;
        }

        if (!viewport.Contains(_currentUserLocation.Latitude, _currentUserLocation.Longitude))
        {
            return;
        }

        if (!TryGetOverlayAnchorPoint(_currentUserLocation.Latitude, _currentUserLocation.Longitude, viewport, insets, out var anchor))
        {
            return;
        }

        var badge = CreateCurrentUserMapBadge();
        AbsoluteLayout.SetLayoutFlags(badge, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutBounds(badge, new Rect(anchor.X - 34d, anchor.Y - 76d, 68d, 76d));
        BubbleLayer.Children.Add(badge);
    }

    private View CreateCurrentUserMapBadge()
    {
        var nickname = _myProfile?.Nickname ?? _loginViewModel.Nickname;
        var displayName = string.IsNullOrWhiteSpace(_myProfile?.DisplayName) ? nickname : _myProfile!.DisplayName!;
        var preview = new PresencePreview
        {
            UserId = _myProfile?.UserId ?? Guid.Empty,
            DisplayName = displayName,
            Nickname = nickname,
            AvatarUrl = _myProfile?.AvatarUrl
        };

        var host = new Grid
        {
            WidthRequest = 68,
            HeightRequest = 76,
            InputTransparent = true
        };

        var glow = new Border
        {
            WidthRequest = 58,
            HeightRequest = 58,
            Background = new RadialGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#8B5CF6").WithAlpha(0.30f), 0f),
                    new GradientStop(Color.FromArgb("#7C3AED").WithAlpha(0.14f), 0.58f),
                    new GradientStop(Color.FromArgb("#7C3AED").WithAlpha(0f), 1f)
                },
                new Point(0.5, 0.5),
                1f),
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 2, 0, 0),
            StrokeShape = new RoundRectangle { CornerRadius = 29 }
        };

        var avatar = CreateAvatarBadge(preview, 44);
        avatar.HorizontalOptions = LayoutOptions.Center;
        avatar.VerticalOptions = LayoutOptions.Start;
        avatar.Margin = new Thickness(0, 8, 0, 0);
        avatar.Stroke = Color.FromArgb("#FFFFFF");
        avatar.StrokeThickness = 3;
        avatar.Shadow = new Shadow
        {
            Brush = new SolidColorBrush(Colors.Black),
            Radius = 18,
            Offset = new Point(0, 8),
            Opacity = 0.16f
        };

        var pointer = new Border
        {
            WidthRequest = 12,
            HeightRequest = 12,
            Rotation = 45,
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#E9D5FF"),
            StrokeThickness = 1,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 46, 0, 0),
            StrokeShape = new RoundRectangle { CornerRadius = 4 }
        };

        host.Children.Add(glow);
        host.Children.Add(pointer);
        host.Children.Add(avatar);
        StartCloudPulse(glow, 0.9d);
        return host;
    }

    private void SetMapStatus(string message, bool isError)
    {
        _viewModel.StatusColor = isError
            ? Color.FromArgb("#B91C1C")
            : Color.FromArgb("#6D28D9");
        _viewModel.StatusMessage = message;
    }

    private static Microsoft.Maui.Controls.Maps.Polygon CreateHexagon(double latitude, double longitude, double radiusMeters, Color fillColor, Color strokeColor, float strokeWidth)
    {
        var polygon = new Microsoft.Maui.Controls.Maps.Polygon
        {
            FillColor = fillColor,
            StrokeColor = strokeColor,
            StrokeWidth = strokeWidth
        };

        for (var i = 0; i < 6; i++)
        {
            var angle = Math.PI / 3 * i + Math.PI / 6;
            var eastMeters = Math.Cos(angle) * radiusMeters;
            var northMeters = Math.Sin(angle) * radiusMeters;
            polygon.Add(OffsetLocation(latitude, longitude, northMeters, eastMeters));
        }

        return polygon;
    }

    private static Location OffsetLocation(double latitude, double longitude, double northMeters, double eastMeters)
    {
        var latOffset = northMeters / 111_320d;
        var lngScale = Math.Cos(latitude * Math.PI / 180d);
        var lngOffset = eastMeters / (111_320d * Math.Max(0.2, lngScale));
        return new Location(latitude + latOffset, longitude + lngOffset);
    }

    private Color ResolveSignalColor(VenueMarker marker)
    {
        if (marker.OpenTables > 0)
        {
            return Color.FromArgb("#7C3AED");
        }

        if (marker.ActiveIntentions > 0)
        {
            return Color.FromArgb("#7C3AED");
        }

        if (marker.ActiveCheckIns > 0)
        {
            return Color.FromArgb("#06B6D4");
        }

        return _viewModel.ResolveBubbleColor(marker.BubbleIntensity);
    }

    private Color ResolveAreaColor(MapArea area)
    {
        if (area.OpenTables > 0)
        {
            return Color.FromArgb("#7C3AED");
        }

        if (area.ActiveIntentions > 0)
        {
            return Color.FromArgb("#7C3AED");
        }

        if (area.ActiveCheckIns > 0)
        {
            return Color.FromArgb("#06B6D4");
        }

        return _viewModel.ResolveBubbleColor(area.BubbleIntensity);
    }

    private Color ResolveClusterColor(IEnumerable<VenueMarker> markers)
    {
        var marker = markers
            .OrderByDescending(x => x.OpenTables > 0)
            .ThenByDescending(x => x.ActiveIntentions > 0)
            .ThenByDescending(x => x.ActiveCheckIns > 0)
            .ThenByDescending(x => x.BubbleIntensity)
            .First();

        return ResolveSignalColor(marker);
    }

    partial void RenderNativeCloudAnnotations(IReadOnlyList<VenueOverlayCluster> clusters);

    private static VenueOverlayCluster CreateSingleMarkerCluster(VenueMarker marker, double x, double y, double size)
    {
        return new VenueOverlayCluster(
            marker.VenueId.ToString("N"),
            new List<VenueMarker> { marker },
            marker.PresencePreview.ToList(),
            marker.PresencePreview.Count,
            GetMarkerPeopleCount(marker),
            1,
            BuildAreaLabel(new[] { marker }),
            x,
            y,
            size,
            1d,
            ResolveSignalColorStatic(marker),
            false,
            marker.Latitude,
            marker.Longitude);
    }

    private static Color ResolveSignalColorStatic(VenueMarker marker)
    {
        if (marker.OpenTables > 0)
        {
            return Color.FromArgb("#7C3AED");
        }

        if (marker.ActiveIntentions > 0)
        {
            return Color.FromArgb("#7C3AED");
        }

        if (marker.ActiveCheckIns > 0)
        {
            return Color.FromArgb("#06B6D4");
        }

        return marker.BubbleIntensity switch
        {
            <= 0 => Colors.Transparent,
            < 25 => Color.FromArgb("#EDE9FE"),
            < 45 => Color.FromArgb("#C4B5FD"),
            < 65 => Color.FromArgb("#A78BFA"),
            < 85 => Color.FromArgb("#7C3AED"),
            _ => Color.FromArgb("#6D28D9")
        };
    }

    private void OnApnsDeviceTokenChanged(object? sender, string token)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await _viewModel.RegisterStoredDeviceTokenAsync();
            }
            catch
            {
                // Push registration is best-effort in local development.
            }
        });
    }

    private readonly record struct OverlayInsets(double Left, double Top, double Right, double Bottom);
    private readonly record struct OverlayMarkerProjection(VenueMarker Marker, double X, double Y, double Size);
    private readonly record struct FogLink(Rect Bounds, double Angle, double Height, Color Color, Color GlowColor, double Intensity);
    private sealed record VenueOverlayCluster(
        string Key,
        List<VenueMarker> Markers,
        List<PresencePreview> PresencePreview,
        int TotalPresenceCount,
        int PeopleCount,
        int VenueCount,
        string AreaLabel,
        double X,
        double Y,
        double Size,
        double BubbleScaleHint,
        Color Color,
        bool IsCluster,
        double Latitude,
        double Longitude)
    {
        public double LayoutWidth => Size * 1.86d;
        public double LayoutHeight => Size * 1.54d + (IsCluster ? 28d : 16d);
        public double AnchorYOffset => IsCluster ? 54d : 42d;
        public double CloudYOffset => IsCluster ? 24d : 16d;
    }

    private enum VenueSheetSnapState
    {
        Teaser,
        Collapsed,
        Expanded
    }
}
