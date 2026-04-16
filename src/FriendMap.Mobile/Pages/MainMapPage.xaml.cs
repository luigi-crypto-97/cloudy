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
    private readonly MainMapViewModel _viewModel;
    private readonly IDevicePermissionService _permissions;
    private bool _permissionsRequested;

    public MainMapPage(MainMapViewModel viewModel, IDevicePermissionService permissions)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _permissions = permissions;
        BindingContext = _viewModel;
        _viewModel.MarkersRefreshed += (_, _) => MainThread.BeginInvokeOnMainThread(RenderMap);
#if FRIENDMAP_APNS_ENABLED
        ApnsDeviceTokenStore.TokenChanged += OnApnsDeviceTokenChanged;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_permissionsRequested)
        {
            _permissionsRequested = true;
            await _viewModel.RequestPermissionsAndRegisterDeviceAsync(_permissions);
        }

        await _viewModel.RefreshAsync();
        RenderMap();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await _viewModel.RefreshAsync();
        RenderMap();
    }

    private void RenderMap()
    {
        NativeMap.Pins.Clear();
        NativeMap.MapElements.Clear();
        BubbleLayer.Children.Clear();

        var markers = _viewModel.Markers
            .Where(x => x.Latitude != 0 && x.Longitude != 0)
            .ToList();
        if (markers.Count == 0)
        {
            NativeMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(45.4642, 9.1900),
                Distance.FromKilometers(8)));
            return;
        }

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
                Radius = Distance.FromMeters(90 + marker.BubbleIntensity * 7),
                StrokeColor = _viewModel.ResolveBubbleColor(marker.BubbleIntensity),
                StrokeWidth = 2,
                FillColor = _viewModel.ResolveBubbleColor(marker.BubbleIntensity).WithAlpha(0.30f)
            });
        }

        MoveToMarkers(markers);
        RenderBubbleOverlay(markers);
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
            var size = 38 + Math.Min(34, marker.BubbleIntensity / 2);

            var bubble = new Border
            {
                WidthRequest = size,
                HeightRequest = size,
                InputTransparent = false,
                BackgroundColor = _viewModel.ResolveBubbleColor(marker.BubbleIntensity),
                StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
                StrokeThickness = 0,
                Content = new Label
                {
                    Text = marker.PeopleEstimate.ToString(),
                    TextColor = Colors.White,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };
            bubble.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() => _viewModel.SelectMarker(marker))
            });

            AbsoluteLayout.SetLayoutFlags(bubble, AbsoluteLayoutFlags.PositionProportional);
            AbsoluteLayout.SetLayoutBounds(bubble, new Rect(x, y, size, size));
            BubbleLayer.Children.Add(bubble);
        }
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
