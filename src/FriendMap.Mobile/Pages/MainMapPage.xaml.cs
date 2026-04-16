using System.ComponentModel;
using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;
using FriendMap.Mobile.ViewModels;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Maps;

namespace FriendMap.Mobile.Pages;

public partial class MainMapPage : ContentPage
{
    private const double HiddenSheetPadding = 28;
    private const double MinimumCollapsedVisibleHeight = 150;

    private readonly MainMapViewModel _viewModel;
    private readonly LoginViewModel _loginViewModel;
    private readonly IDevicePermissionService _permissions;
    private bool _permissionsRequested;
    private double _sheetPanStartY;

    public MainMapPage(MainMapViewModel viewModel, LoginViewModel loginViewModel, IDevicePermissionService permissions)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _loginViewModel = loginViewModel;
        _permissions = permissions;
        BindingContext = _viewModel;
        _viewModel.MarkersRefreshed += (_, _) => MainThread.BeginInvokeOnMainThread(RenderMap);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SizeChanged += OnPageSizeChanged;
#if FRIENDMAP_APNS_ENABLED
        ApnsDeviceTokenStore.TokenChanged += OnApnsDeviceTokenChanged;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            if (!_permissionsRequested)
            {
                _permissionsRequested = true;
                await _viewModel.RequestPermissionsAndRegisterDeviceAsync(_permissions);
            }

            await _viewModel.RefreshAsync();
            RenderMap();
            await SyncVenueSheetAsync(animated: false);
        }
        catch (Exception ex)
        {
            _viewModel.SetStatusMessage($"Errore durante il caricamento della mappa: {ex.Message}");
            RenderMap();
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        try
        {
            await _viewModel.RefreshAsync();
            RenderMap();
        }
        catch (Exception ex)
        {
            _viewModel.SetStatusMessage($"Errore durante l'aggiornamento: {ex.Message}");
            RenderMap();
        }
    }

    private async void OnServerClicked(object sender, EventArgs e)
    {
        _loginViewModel.PauseAutoRestoreOnce();
        await Shell.Current.Navigation.PopToRootAsync();
    }

    private void OnMapClicked(object? sender, MapClickedEventArgs e)
    {
        _viewModel.ClearSelection();
    }

    private void OnFocusMarkersClicked(object sender, EventArgs e)
    {
        var markers = GetRenderableMarkers();
        if (markers.Count == 0)
        {
            return;
        }

        MoveToMarkers(markers);
    }

    private async void OnVenueSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!_viewModel.HasSelectedMarker)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _sheetPanStartY = VenueSheet.TranslationY;
                break;
            case GestureStatus.Running:
                VenueSheet.TranslationY = ClampSheetOffset(_sheetPanStartY + e.TotalY);
                break;
            case GestureStatus.Canceled:
            case GestureStatus.Completed:
                var target = ResolveNearestSheetOffset(VenueSheet.TranslationY);
                if (target >= GetHiddenSheetOffset() - 1)
                {
                    await AnimateVenueSheetToAsync(target, true);
                    _viewModel.ClearSelection();
                    return;
                }

                await AnimateVenueSheetToAsync(target, true);
                break;
        }
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (!_viewModel.HasSelectedMarker)
        {
            VenueSheet.TranslationY = GetHiddenSheetOffset();
        }
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainMapViewModel.SelectedMarker) or nameof(MainMapViewModel.HasSelectedMarker))
        {
            RenderSelectedPresencePreview();
            await SyncVenueSheetAsync(animated: true);
        }
    }

    private void RenderMap()
    {
        NativeMap.Pins.Clear();
        NativeMap.MapElements.Clear();
        BubbleLayer.Children.Clear();
        RenderSelectedPresencePreview();

        var markers = GetRenderableMarkers();
        if (markers.Count == 0)
        {
            NativeMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(45.4642, 9.1900),
                Distance.FromKilometers(8)));
            return;
        }

        RenderHeatZones(markers);

        foreach (var marker in markers)
        {
            var location = new Location(marker.Latitude, marker.Longitude);
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
                _viewModel.SelectMarker(marker);
            };
            NativeMap.Pins.Add(pin);

            NativeMap.MapElements.Add(new Circle
            {
                Center = location,
                Radius = Distance.FromMeters(75 + marker.BubbleIntensity * 5),
                StrokeColor = ResolveSignalColor(marker).WithAlpha(0.55f),
                StrokeWidth = 2,
                FillColor = ResolveSignalColor(marker).WithAlpha(0.09f)
            });
        }

        MoveToMarkers(markers);
        RenderBubbleOverlay(markers);
    }

    private List<VenueMarker> GetRenderableMarkers()
    {
        return _viewModel.Markers
            .Where(x => x.Latitude != 0 && x.Longitude != 0)
            .ToList();
    }

    private void MoveToMarkers(List<VenueMarker> markers)
    {
        var centerLat = markers.Average(x => x.Latitude);
        var centerLng = markers.Average(x => x.Longitude);
        var latSpan = Math.Max(0.02, markers.Max(x => x.Latitude) - markers.Min(x => x.Latitude));
        var lngSpan = Math.Max(0.02, markers.Max(x => x.Longitude) - markers.Min(x => x.Longitude));
        var radiusKm = Math.Max(latSpan, lngSpan) * 111;

        NativeMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(centerLat, centerLng),
            Distance.FromKilometers(Math.Clamp(radiusKm, 2, 20))));
    }

    private void RenderHeatZones(List<VenueMarker> markers)
    {
        foreach (var marker in markers.Where(x => x.BubbleIntensity > 0 || x.ActiveCheckIns > 0 || x.ActiveIntentions > 0 || x.OpenTables > 0))
        {
            var baseColor = ResolveSignalColor(marker);
            var outerZone = CreateHexagon(
                marker.Latitude,
                marker.Longitude,
                140 + marker.BubbleIntensity * 4.5,
                baseColor.WithAlpha(0.14f),
                baseColor.WithAlpha(0.45f),
                2);

            NativeMap.MapElements.Add(outerZone);

            if (marker.BubbleIntensity >= 35)
            {
                var innerZone = CreateHexagon(
                    marker.Latitude,
                    marker.Longitude,
                    82 + marker.BubbleIntensity * 2.2,
                    baseColor.WithAlpha(0.18f),
                    baseColor.WithAlpha(0.55f),
                    1.5f);

                NativeMap.MapElements.Add(innerZone);
            }
        }
    }

    private void RenderBubbleOverlay(List<VenueMarker> markers)
    {
        var minLat = markers.Min(x => x.Latitude);
        var maxLat = markers.Max(x => x.Latitude);
        var minLng = markers.Min(x => x.Longitude);
        var maxLng = markers.Max(x => x.Longitude);
        var latRange = Math.Max(0.0001, maxLat - minLat);
        var lngRange = Math.Max(0.0001, maxLng - minLng);

        foreach (var marker in markers)
        {
            var x = 0.16 + ((marker.Longitude - minLng) / lngRange * 0.68);
            var y = 0.78 - ((marker.Latitude - minLat) / latRange * 0.56);
            var size = 42 + Math.Min(28, marker.BubbleIntensity / 3.2);

            var bubbleColor = ResolveSignalColor(marker);
            var bubble = new Border
            {
                WidthRequest = size,
                HeightRequest = size,
                InputTransparent = false,
                BackgroundColor = Colors.White,
                Stroke = bubbleColor,
                StrokeThickness = 3,
                StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
                Shadow = new Shadow
                {
                    Brush = new SolidColorBrush(Colors.Black),
                    Radius = 18,
                    Offset = new Point(0, 8),
                    Opacity = 0.14f
                },
                Content = new Grid
                {
                    Children =
                    {
                        new Border
                        {
                            Margin = 5,
                            BackgroundColor = bubbleColor.WithAlpha(0.15f),
                            StrokeThickness = 0,
                            StrokeShape = new RoundRectangle { CornerRadius = (size - 10) / 2 }
                        },
                        new Label
                        {
                            Text = Math.Max(marker.PeopleEstimate, marker.ActiveCheckIns + marker.ActiveIntentions).ToString(),
                            TextColor = bubbleColor,
                            FontAttributes = FontAttributes.Bold,
                            FontSize = 14,
                            HorizontalTextAlignment = TextAlignment.Center,
                            VerticalTextAlignment = TextAlignment.Center
                        }
                    }
                }
            };

            bubble.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() => _viewModel.SelectMarker(marker))
            });

            AbsoluteLayout.SetLayoutFlags(bubble, AbsoluteLayoutFlags.PositionProportional);
            AbsoluteLayout.SetLayoutBounds(bubble, new Rect(x, y, size, size));
            BubbleLayer.Children.Add(bubble);

            RenderPresenceOverlay(marker, x, y, size);
        }
    }

    private void RenderPresenceOverlay(VenueMarker marker, double x, double y, double size)
    {
        if (marker.PresencePreview.Count == 0)
        {
            return;
        }

        var stack = new HorizontalStackLayout
        {
            Spacing = -8,
            InputTransparent = true
        };

        foreach (var preview in marker.PresencePreview.Take(3))
        {
            stack.Children.Add(CreateAvatarBadge(preview, 28));
        }

        if (marker.PresencePreview.Count > 3)
        {
            stack.Children.Add(CreateOverflowAvatar(marker.PresencePreview.Count - 3, 28));
        }

        var stackWidth = 34 + Math.Min(marker.PresencePreview.Count - 1, 2) * 20;
        var positionX = Math.Clamp(x + 0.09, 0.14, 0.88);
        var positionY = Math.Clamp(y - 0.07, 0.10, 0.86);

        AbsoluteLayout.SetLayoutFlags(stack, AbsoluteLayoutFlags.PositionProportional);
        AbsoluteLayout.SetLayoutBounds(stack, new Rect(positionX, positionY, stackWidth, 30));
        BubbleLayer.Children.Add(stack);
    }

    private void RenderSelectedPresencePreview()
    {
        SheetPresenceStack.Children.Clear();

        var previews = _viewModel.SelectedMarker?.PresencePreview;
        if (previews is null || previews.Count == 0)
        {
            return;
        }

        foreach (var preview in previews.Take(4))
        {
            SheetPresenceStack.Children.Add(CreateAvatarBadge(preview, 34));
        }

        if (previews.Count > 4)
        {
            SheetPresenceStack.Children.Add(CreateOverflowAvatar(previews.Count - 4, 34));
        }
    }

    private static Border CreateOverflowAvatar(int extraCount, double size)
    {
        return new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            BackgroundColor = Color.FromArgb("#E7EEF8"),
            Stroke = Colors.White,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
            Content = new Label
            {
                Text = $"+{extraCount}",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1D4ED8"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
    }

    private static Border CreateAvatarBadge(PresencePreview preview, double size)
    {
        var background = ResolveAvatarBackground(preview.DisplayName, preview.Nickname);
        return new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            BackgroundColor = background,
            Stroke = Colors.White,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Colors.Black),
                Radius = 10,
                Offset = new Point(0, 4),
                Opacity = 0.12f
            },
            Content = new Label
            {
                Text = BuildInitials(preview.DisplayName, preview.Nickname),
                FontSize = size >= 34 ? 12 : 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
    }

    private static string BuildInitials(string displayName, string nickname)
    {
        var source = string.IsNullOrWhiteSpace(displayName) ? nickname : displayName;
        var parts = source
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0)
            .ToList();

        if (parts.Count >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }

        if (parts.Count == 1 && parts[0].Length >= 2)
        {
            return parts[0][..2].ToUpperInvariant();
        }

        return source[..Math.Min(2, source.Length)].ToUpperInvariant();
    }

    private static Color ResolveAvatarBackground(string displayName, string nickname)
    {
        var palette = new[]
        {
            "#3B82F6",
            "#8B5CF6",
            "#EC4899",
            "#14B8A6",
            "#F97316",
            "#0EA5E9"
        };

        var seed = string.IsNullOrWhiteSpace(displayName) ? nickname : displayName;
        var hash = seed.Aggregate(17, (current, ch) => current * 31 + ch);
        return Color.FromArgb(palette[Math.Abs(hash) % palette.Length]);
    }

    private async Task SyncVenueSheetAsync(bool animated)
    {
        if (_viewModel.HasSelectedMarker)
        {
            VenueSheet.IsVisible = true;
            var target = VenueSheet.TranslationY >= GetHiddenSheetOffset() - 1
                ? GetCollapsedSheetOffset()
                : Math.Min(VenueSheet.TranslationY, GetCollapsedSheetOffset());

            await AnimateVenueSheetToAsync(target, animated);
            return;
        }

        if (!VenueSheet.IsVisible)
        {
            VenueSheet.TranslationY = GetHiddenSheetOffset();
            return;
        }

        await AnimateVenueSheetToAsync(GetHiddenSheetOffset(), animated);
        VenueSheet.IsVisible = false;
    }

    private async Task AnimateVenueSheetToAsync(double target, bool animated)
    {
        target = ClampSheetOffset(target);
        if (!animated)
        {
            VenueSheet.TranslationY = target;
            return;
        }

        await VenueSheet.TranslateTo(0, target, 220, Easing.CubicOut);
    }

    private double ResolveNearestSheetOffset(double currentOffset)
    {
        var offsets = new[] { 0d, GetCollapsedSheetOffset(), GetHiddenSheetOffset() };
        return offsets.OrderBy(x => Math.Abs(x - currentOffset)).First();
    }

    private double ClampSheetOffset(double value)
    {
        return Math.Clamp(value, 0, GetHiddenSheetOffset());
    }

    private double GetHiddenSheetOffset()
    {
        return Math.Max(320, VenueSheet.Height + HiddenSheetPadding);
    }

    private double GetCollapsedSheetOffset()
    {
        var hidden = GetHiddenSheetOffset();
        return Math.Clamp(hidden - MinimumCollapsedVisibleHeight, 92, hidden - 32);
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
            return Color.FromArgb("#2563EB");
        }

        if (marker.ActiveIntentions > 0)
        {
            return Color.FromArgb("#7C3AED");
        }

        if (marker.ActiveCheckIns > 0)
        {
            return Color.FromArgb("#0EA5E9");
        }

        return _viewModel.ResolveBubbleColor(marker.BubbleIntensity);
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
}
