#if IOS
using CoreAnimation;
using CoreGraphics;
using CoreLocation;
using Foundation;
using MapKit;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Platform;
using UIKit;

namespace FriendMap.Mobile.Pages;

public partial class MainMapPage
{
    private MKMapView? _platformMapView;
    private FriendMapCloudMapDelegate? _cloudMapDelegate;
    private readonly Dictionary<string, VenueOverlayCluster> _nativeCloudIndex = new(StringComparer.Ordinal);

    partial void RenderNativeCloudAnnotations(IReadOnlyList<VenueOverlayCluster> clusters)
    {
        var mapView = TryGetPlatformMapView();
        if (mapView is null)
        {
            return;
        }

        EnsureCloudDelegate(mapView);

        var existingAnnotations = mapView.Annotations?
            .OfType<FriendMapCloudAnnotation>()
            .ToArray() ?? Array.Empty<FriendMapCloudAnnotation>();

        if (existingAnnotations.Length > 0)
        {
            mapView.RemoveAnnotations(existingAnnotations);
        }

        _nativeCloudIndex.Clear();
        if (clusters.Count == 0)
        {
            return;
        }

        var annotations = new List<FriendMapCloudAnnotation>(clusters.Count);
        foreach (var cluster in clusters)
        {
            _nativeCloudIndex[cluster.Key] = cluster;
            annotations.Add(new FriendMapCloudAnnotation(
                cluster.Key,
                new CLLocationCoordinate2D(cluster.Latitude, cluster.Longitude),
                cluster.PeopleCount,
                cluster.Color.ToPlatform(),
                cluster.IsCluster,
                _selectedAreaClusterKey == cluster.Key));
        }

        mapView.AddAnnotations(annotations.ToArray());
    }

    private MKMapView? TryGetPlatformMapView()
    {
        return NativeMap.Handler?.PlatformView as MKMapView;
    }

    private void EnsureCloudDelegate(MKMapView mapView)
    {
        if (_platformMapView == mapView && _cloudMapDelegate is not null)
        {
            return;
        }

        var existingDelegate = mapView.Delegate;
        if (existingDelegate is FriendMapCloudMapDelegate currentDelegate)
        {
            _cloudMapDelegate = currentDelegate;
            _platformMapView = mapView;
            return;
        }

        _cloudMapDelegate = new FriendMapCloudMapDelegate(this, existingDelegate as MKMapViewDelegate);
        mapView.Delegate = _cloudMapDelegate;
        _platformMapView = mapView;
    }

    private async Task OnNativeCloudAnnotationTappedAsync(string key)
    {
        if (_nativeCloudIndex.TryGetValue(key, out var cluster))
        {
            await OnClusterTappedAsync(cluster);
        }
    }

    private sealed class FriendMapCloudMapDelegate : MKMapViewDelegate
    {
        private const string ReuseId = "FriendMapCloudAnnotation";
        private readonly MainMapPage _page;
        private readonly MKMapViewDelegate? _innerDelegate;

        public FriendMapCloudMapDelegate(MainMapPage page, MKMapViewDelegate? innerDelegate)
        {
            _page = page;
            _innerDelegate = innerDelegate;
        }

        public override MKAnnotationView? GetViewForAnnotation(MKMapView mapView, IMKAnnotation annotation)
        {
            if (annotation is FriendMapCloudAnnotation cloudAnnotation)
            {
                var view = mapView.DequeueReusableAnnotation(ReuseId) as FriendMapCloudAnnotationView
                    ?? new FriendMapCloudAnnotationView(annotation, ReuseId);
                view.Apply(cloudAnnotation);
                return view;
            }

            return _innerDelegate?.GetViewForAnnotation(mapView, annotation);
        }

        public override void DidSelectAnnotationView(MKMapView mapView, MKAnnotationView view)
        {
            if (view.Annotation is FriendMapCloudAnnotation cloudAnnotation)
            {
                mapView.DeselectAnnotation(cloudAnnotation, false);
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await _page.OnNativeCloudAnnotationTappedAsync(cloudAnnotation.Key);
                });
                return;
            }

            _innerDelegate?.DidSelectAnnotationView(mapView, view);
        }
    }

    private sealed class FriendMapCloudAnnotation : MKPointAnnotation
    {
        public FriendMapCloudAnnotation(
            string key,
            CLLocationCoordinate2D coordinate,
            int peopleCount,
            UIColor tintColor,
            bool isCluster,
            bool isSelectedArea)
        {
            Key = key;
            Coordinate = coordinate;
            PeopleCount = peopleCount;
            TintColor = tintColor;
            IsCluster = isCluster;
            IsSelectedArea = isSelectedArea;
        }

        public string Key { get; }
        public int PeopleCount { get; }
        public UIColor TintColor { get; }
        public bool IsCluster { get; }
        public bool IsSelectedArea { get; }
    }

    private sealed class FriendMapCloudAnnotationView : MKAnnotationView
    {
        private readonly UIView _host;
        private readonly UILabel _countLabel;
        private readonly List<UIView> _puffs = new();
        private readonly List<UIView> _highlights = new();

        public FriendMapCloudAnnotationView(IMKAnnotation annotation, string reuseIdentifier)
            : base(annotation, reuseIdentifier)
        {
            CanShowCallout = false;
            BackgroundColor = UIColor.Clear;
            Opaque = false;
            CenterOffset = new CGPoint(0, -6);

            _host = new UIView
            {
                BackgroundColor = UIColor.Clear,
                UserInteractionEnabled = false
            };

            for (var i = 0; i < 4; i++)
            {
                var puff = new UIView
                {
                    BackgroundColor = UIColor.White,
                    UserInteractionEnabled = false
                };
                puff.Layer.ShadowColor = UIColor.Black.CGColor;
                puff.Layer.ShadowOffset = new CGSize(0, 6);
                puff.Layer.ShadowRadius = 12;
                puff.Layer.ShadowOpacity = 0.10f;
                _host.AddSubview(puff);
                _puffs.Add(puff);
            }

            for (var i = 0; i < 2; i++)
            {
                var highlight = new UIView
                {
                    BackgroundColor = UIColor.White.ColorWithAlpha(0.62f),
                    UserInteractionEnabled = false
                };
                _host.AddSubview(highlight);
                _highlights.Add(highlight);
            }

            _countLabel = new UILabel
            {
                TextAlignment = UITextAlignment.Center,
                BackgroundColor = UIColor.Clear,
                Font = UIFont.SystemFontOfSize(16, UIFontWeight.Bold),
                UserInteractionEnabled = false
            };
            _host.AddSubview(_countLabel);

            AddSubview(_host);
        }

        public void Apply(FriendMapCloudAnnotation annotation)
        {
            Annotation = annotation;

            var cloudSize = annotation.IsCluster ? 78d : 68d;
            var width = cloudSize;
            var height = annotation.IsCluster ? cloudSize + 18d : cloudSize;
            Frame = new CGRect(0, 0, width, height);
            Bounds = new CGRect(0, 0, width, height);
            _host.Frame = Bounds;

            var tint = annotation.TintColor;
            var stroke = Blend(tint, UIColor.White, 0.28f);
            var fill = Blend(UIColor.White, tint.ColorWithAlpha(annotation.IsSelectedArea ? 0.28f : 0.18f), 0.26f);
            var shadowOpacity = annotation.IsSelectedArea ? 0.18f : 0.10f;
            var cloudYOffset = annotation.IsCluster ? 16d : 0d;

            LayoutPuff(_puffs[0], new CGRect(width * 0.08d, cloudYOffset + cloudSize * 0.24d, cloudSize * 0.38d, cloudSize * 0.38d), fill, stroke, shadowOpacity, -0.16f, 0.18f);
            LayoutPuff(_puffs[1], new CGRect(width * 0.20d, cloudYOffset + cloudSize * 0.04d, cloudSize * 0.50d, cloudSize * 0.50d), fill, stroke, shadowOpacity, 0.14f, -0.10f);
            LayoutPuff(_puffs[2], new CGRect(width * 0.49d, cloudYOffset + cloudSize * 0.18d, cloudSize * 0.42d, cloudSize * 0.42d), fill, stroke, shadowOpacity, -0.08f, 0.12f);
            LayoutPuff(_puffs[3], new CGRect(width * 0.18d, cloudYOffset + cloudSize * 0.38d, cloudSize * 0.60d, cloudSize * 0.26d), fill, stroke, shadowOpacity, 0.05f, 0f);

            LayoutHighlight(_highlights[0], new CGRect(width * 0.19d, cloudYOffset + cloudSize * 0.16d, cloudSize * 0.22d, cloudSize * 0.08d));
            LayoutHighlight(_highlights[1], new CGRect(width * 0.47d, cloudYOffset + cloudSize * 0.24d, cloudSize * 0.16d, cloudSize * 0.07d));

            _countLabel.Frame = new CGRect(0, cloudYOffset + cloudSize * 0.17d, width, cloudSize * 0.44d);
            _countLabel.Text = annotation.PeopleCount.ToString();
            _countLabel.TextColor = tint;
            _countLabel.Font = UIFont.SystemFontOfSize(annotation.IsCluster ? 17 : 15, UIFontWeight.Bold);

            Layer.ShadowColor = UIColor.Black.CGColor;
            Layer.ShadowOffset = new CGSize(0, annotation.IsSelectedArea ? 10 : 8);
            Layer.ShadowRadius = annotation.IsSelectedArea ? 18 : 14;
            Layer.ShadowOpacity = annotation.IsSelectedArea ? 0.16f : 0.11f;

            var transform = CATransform3D.MakeRotation(annotation.IsSelectedArea ? 0.22f : 0.18f, 1f, -0.75f, 0f);
            transform.M34 = -1f / 600f;
            Layer.Transform = transform;
        }

        private static void LayoutHighlight(UIView view, CGRect frame)
        {
            view.Frame = frame;
            view.Layer.CornerRadius = (nfloat)(Math.Min(frame.Width, frame.Height) / 2d);
        }

        private static void LayoutPuff(UIView view, CGRect frame, UIColor fill, UIColor stroke, float shadowOpacity, float rotationX, float rotationY)
        {
            view.Frame = frame;
            view.Layer.CornerRadius = (nfloat)(Math.Min(frame.Width, frame.Height) / 2d);
            view.Layer.BorderWidth = 1.8f;
            view.Layer.BorderColor = stroke.CGColor;
            view.BackgroundColor = fill;
            view.Layer.ShadowOpacity = shadowOpacity;

            var transform = CATransform3D.MakeRotation(rotationX + rotationY, 1f, 0.6f, 0f);
            transform.M34 = -1f / 560f;
            view.Layer.Transform = transform;
        }

        private static UIColor Blend(UIColor first, UIColor second, nfloat amount)
        {
            first.GetRGBA(out var r1, out var g1, out var b1, out var a1);
            second.GetRGBA(out var r2, out var g2, out var b2, out var a2);

            return UIColor.FromRGBA(
                r1 + (r2 - r1) * amount,
                g1 + (g2 - g1) * amount,
                b1 + (b2 - b1) * amount,
                a1 + (a2 - a1) * amount);
        }
    }
}
#endif
