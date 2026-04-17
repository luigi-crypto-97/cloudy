#if IOS
using CoreLocation;
using MapKit;

namespace FriendMap.Mobile.Pages;

public partial class MainMapPage
{
    partial void RenderNativeCloudAnnotations(IReadOnlyList<VenueOverlayCluster> clusters)
    {
        // Native annotations are intentionally disabled on iOS.
        // MAUI owns the MKMapView delegate internally; overriding it causes a runtime crash.
    }

    private bool TryProjectCoordinateToScreenIos(double latitude, double longitude, out Microsoft.Maui.Graphics.Point point)
    {
        point = default;

        if (NativeMap.Handler?.PlatformView is not MKMapView mapView)
        {
            return false;
        }

        var projected = mapView.ConvertCoordinate(
            new CLLocationCoordinate2D(latitude, longitude),
            mapView);

        if (double.IsNaN(projected.X) || double.IsNaN(projected.Y) ||
            double.IsInfinity(projected.X) || double.IsInfinity(projected.Y))
        {
            return false;
        }

        point = new Microsoft.Maui.Graphics.Point(projected.X, projected.Y);
        return true;
    }
}
#endif
