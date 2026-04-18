using System.ComponentModel;
using System.Text.Json;
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
    private const bool EnableNativeIosCloudAnnotations = false;
    private const double HiddenSheetPadding = 28;
    private const double MinimumTeaserVisibleHeight = 116;
    private const double MinimumCollapsedVisibleHeight = 288;
    private const int ViewportRefreshDelayMs = 1000;
    private const int OverlaySyncIntervalMs = 1000;
    private static readonly TimeSpan CurrentUserLocationCacheDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SocialOverlayCacheDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MapLayerCacheDuration = TimeSpan.FromMinutes(2);

    private readonly MainMapViewModel _viewModel;
    private readonly LoginViewModel _loginViewModel;
    private readonly IDevicePermissionService _permissions;
    private readonly ApiClient _apiClient;

    private CancellationTokenSource? _viewportRefreshCts;
    private CancellationTokenSource? _discoveryRefreshCts;
    private SocialHub? _socialHub;
    private SocialMeState? _socialMeState;
    private IReadOnlyList<SocialTableSummary> _socialTables = Array.Empty<SocialTableSummary>();
    private SocialTableThread? _activeTableThread;
    private SocialTableSummary? _activeTableSummary;
    private VenueOverlayCluster? _activePresenceCluster;
    private VenueOverlayCluster? _activeAreaCluster;
    private PresencePreview? _activeProfilePreview;
    private UserProfile? _activeProfile;
    private string? _selectedAreaClusterKey;
    private bool _isQuickActionRailOpen;
    private bool _isLegendPanelOpen;
    private bool _isContrastModeEnabled;
    private bool _isSocialBusy;
    private bool _isSocialLoading;
    private bool _isTableBusy;
    private bool _isApplyingSocialState;
    private bool _permissionsRequested;
    private bool _isProfileActionBusy;
    private bool _shouldAutoFocusOnNextRender = true;
    private bool _isRefreshingCurrentUserLocation;
    private double _sheetPanStartY;
    private MapViewport? _lastRequestedViewport;
    private MapViewport? _lastOverlayViewport;
    private Location? _currentUserLocation;
    private DateTimeOffset _lastCurrentUserLocationRefreshUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _mapLayerLastRefreshUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _suspendViewportRefreshUntilUtc = DateTimeOffset.MinValue;
    private VenueSheetSnapState _sheetSnapState = VenueSheetSnapState.Teaser;
    private IDispatcherTimer? _overlaySyncTimer;

    public MainMapPage(MainMapViewModel viewModel, LoginViewModel loginViewModel, IDevicePermissionService permissions, ApiClient apiClient)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _loginViewModel = loginViewModel;
        _permissions = permissions;
        _apiClient = apiClient;
        BindingContext = _viewModel;
        _viewModel.MarkersRefreshed += (_, _) => MainThread.BeginInvokeOnMainThread(RenderMap);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        NativeMap.MapClicked += OnMapClicked;
        NativeMap.PropertyChanged += OnNativeMapPropertyChanged;
        SizeChanged += OnPageSizeChanged;
        EditGenderPicker.ItemsSource = new[] { "undisclosed", "female", "male", "non-binary" };
        UpdateDiscoveryFilterVisualState();
        ApplyMapMood();
#if FRIENDMAP_APNS_ENABLED
        ApnsDeviceTokenStore.TokenChanged += OnApnsDeviceTokenChanged;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartOverlaySyncTimer();

        try
        {
            if (!_permissionsRequested)
            {
                _permissionsRequested = true;
                await _viewModel.RequestPermissionsAndRegisterDeviceAsync(_permissions);
            }

            EnsureInitialMapRegion();
            _ = WarmPersonalMapContextAsync();
            await Task.Delay(250);
            var mapCacheStale = (DateTimeOffset.UtcNow - _mapLayerLastRefreshUtc) > MapLayerCacheDuration;
            await RefreshForCurrentViewportAsync(force: mapCacheStale || _viewModel.Markers.Count == 0, centerOnMarkers: true);
            await SyncVenueSheetAsync(animated: false);
        }
        catch (Exception ex)
        {
            _viewModel.SetStatusMessage($"Errore durante il caricamento della mappa: {ex.Message}");
            RenderMap();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopOverlaySyncTimer();
        CancelPendingViewportRefresh();
        CancelPendingDiscoveryRefresh();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        try
        {
            await RefreshForCurrentViewportAsync(force: true, centerOnMarkers: false);
        }
        catch (Exception ex)
        {
            _viewModel.SetStatusMessage($"Errore durante l'aggiornamento: {ex.Message}");
            RenderMap();
        }
    }

    private async void OnServerClicked(object sender, EventArgs e)
    {
        await HideQuickActionRailAsync(animated: false);
        _loginViewModel.PauseAutoRestoreOnce();
        await HidePresenceOverlayAsync(animated: false);
        await HideProfileOverlayAsync(animated: false);
        await HideVenueDetailOverlayAsync(animated: false);
        await HideSocialOverlayAsync(animated: false);
        await HideTableOverlayAsync(animated: false);
        await HideEditProfileOverlayAsync(animated: false);
        await HideDirectMessageOverlayAsync(animated: false);
        ClearAreaSelection();
        await Shell.Current.Navigation.PopToRootAsync();
    }

    private async void OnSocialClicked(object sender, EventArgs e)
    {
        await HideQuickActionRailAsync(animated: true);
        await ShowSocialOverlayAsync(forceRefresh: true);
    }

    private async void OnQuickMenuToggleClicked(object? sender, EventArgs e)
    {
        if (_isQuickActionRailOpen)
        {
            await HideQuickActionRailAsync(animated: true);
            return;
        }

        await ShowQuickActionRailAsync();
    }

    private async void OnQuickFiltersToggleClicked(object? sender, EventArgs e)
    {
        DiscoveryChromePanel.IsVisible = true;
        DiscoveryFiltersPanel.IsVisible = !DiscoveryFiltersPanel.IsVisible;

        if (DiscoveryFiltersPanel.IsVisible)
        {
            SetMapStatus("Filtri aperti.", false);
        }
        else
        {
            SetMapStatus(string.Empty, false);
            if (string.IsNullOrWhiteSpace(DiscoverySearchEntry.Text))
            {
                DiscoveryChromePanel.IsVisible = false;
            }
        }

        await HideQuickActionRailAsync(animated: true);
        _lastOverlayViewport = null;
        RenderViewportOverlay();
    }

    private async void OnQuickSearchToggleClicked(object? sender, EventArgs e)
    {
        var shouldOpen = !DiscoveryChromePanel.IsVisible;
        DiscoveryChromePanel.IsVisible = shouldOpen;
        if (!shouldOpen)
        {
            DiscoveryFiltersPanel.IsVisible = false;
            await HideQuickActionRailAsync(animated: true);
            _lastOverlayViewport = null;
            RenderViewportOverlay();
            return;
        }

        await HideQuickActionRailAsync(animated: true);
        await Task.Delay(60);
        DiscoverySearchEntry.Focus();
        _lastOverlayViewport = null;
        RenderViewportOverlay();
    }

    private async void OnLegendToggleClicked(object? sender, EventArgs e)
    {
        if (_isLegendPanelOpen)
        {
            await HideLegendPanelAsync(animated: true);
            await HideQuickActionRailAsync(animated: true);
            return;
        }

        DiscoveryFiltersPanel.IsVisible = false;
        if (string.IsNullOrWhiteSpace(DiscoverySearchEntry.Text))
        {
            DiscoveryChromePanel.IsVisible = false;
        }

        await HideQuickActionRailAsync(animated: true);
        await ShowLegendPanelAsync();
    }

    private async void OnContrastModeToggleClicked(object? sender, EventArgs e)
    {
        _isContrastModeEnabled = !_isContrastModeEnabled;
        ApplyMapMood();
        await HideQuickActionRailAsync(animated: true);
        _lastOverlayViewport = null;
        RenderViewportOverlay();
    }

    private void OnDiscoverySearchChanged(object? sender, TextChangedEventArgs e)
    {
        DiscoveryChromePanel.IsVisible = !string.IsNullOrWhiteSpace(e.NewTextValue) || DiscoveryFiltersPanel.IsVisible;
        ApplyDiscoveryFilters(
            e.NewTextValue ?? string.Empty,
            _viewModel.SelectedCategory,
            _viewModel.OpenNowOnly,
            _viewModel.MaxDistanceKm);
    }

    private void OnCategoryAllClicked(object? sender, EventArgs e)
    {
        ApplyDiscoveryFilters(
            DiscoverySearchEntry.Text ?? string.Empty,
            "all",
            _viewModel.OpenNowOnly,
            _viewModel.MaxDistanceKm);
    }

    private void OnCategoryBarsClicked(object? sender, EventArgs e)
    {
        ApplyDiscoveryFilters(
            DiscoverySearchEntry.Text ?? string.Empty,
            "bar",
            _viewModel.OpenNowOnly,
            _viewModel.MaxDistanceKm);
    }

    private void OnCategoryFoodClicked(object? sender, EventArgs e)
    {
        ApplyDiscoveryFilters(
            DiscoverySearchEntry.Text ?? string.Empty,
            "food",
            _viewModel.OpenNowOnly,
            _viewModel.MaxDistanceKm);
    }

    private void OnCategoryCafeClicked(object? sender, EventArgs e)
    {
        ApplyDiscoveryFilters(
            DiscoverySearchEntry.Text ?? string.Empty,
            "cafe",
            _viewModel.OpenNowOnly,
            _viewModel.MaxDistanceKm);
    }

    private void OnCategoryNightlifeClicked(object? sender, EventArgs e)
    {
        ApplyDiscoveryFilters(
            DiscoverySearchEntry.Text ?? string.Empty,
            "nightlife",
            _viewModel.OpenNowOnly,
            _viewModel.MaxDistanceKm);
    }

    private void OnInterestsFilterClicked(object? sender, EventArgs e)
    {
        // TODO: Show interests picker
        ApplyDiscoveryFilters(
            DiscoverySearchEntry.Text ?? string.Empty,
            _viewModel.SelectedCategory,
            _viewModel.OpenNowOnly,
            _viewModel.MaxDistanceKm);
    }

    private void OnOpenNowToggleClicked(object? sender, EventArgs e)
    {
        ApplyDiscoveryFilters(
            DiscoverySearchEntry.Text ?? string.Empty,
            _viewModel.SelectedCategory,
            !_viewModel.OpenNowOnly,
            _viewModel.MaxDistanceKm);
    }

    private void OnDistanceFilterClicked(object? sender, EventArgs e)
    {
        ApplyDiscoveryFilters(
            DiscoverySearchEntry.Text ?? string.Empty,
            _viewModel.SelectedCategory,
            _viewModel.OpenNowOnly,
            GetNextDistanceFilter(_viewModel.MaxDistanceKm));
    }

    private void OnMapClicked(object? sender, MapClickedEventArgs e)
    {
        _viewModel.ClearSelection();
        _ = HideQuickActionRailAsync(animated: true);
        _ = HideLegendPanelAsync(animated: true);
        DiscoveryFiltersPanel.IsVisible = false;
        if (string.IsNullOrWhiteSpace(DiscoverySearchEntry.Text))
        {
            DiscoveryChromePanel.IsVisible = false;
        }
        _ = HidePresenceOverlayAsync(animated: true);
        _ = HideProfileOverlayAsync(animated: true);
        _ = HideVenueDetailOverlayAsync(animated: true);
        ClearAreaSelection();
        RenderViewportOverlay();
    }

    private void OnFocusMarkersClicked(object sender, EventArgs e)
    {
        var markers = GetRenderableMarkers();
        if (markers.Count == 0)
        {
            return;
        }

        MoveToMarkers(markers, suppressViewportRefresh: true);
    }

    private async void OnCenterUserLocationClicked(object sender, EventArgs e)
    {
        await RefreshCurrentUserLocationAsync(showErrors: true, centerMap: true, force: true);
    }

    private async void OnSelectedVenueCallClicked(object? sender, EventArgs e)
    {
        var phoneNumber = _viewModel.SelectedMarker?.PhoneNumber?.Trim();
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return;
        }

        try
        {
            var normalized = phoneNumber.Replace(" ", string.Empty);
            await Launcher.Default.OpenAsync(new Uri($"tel:{normalized}"));
        }
        catch (Exception ex)
        {
            SetMapStatus($"Impossibile aprire il numero: {ex.Message}", true);
        }
    }

    private async void OnSelectedVenueWebsiteClicked(object? sender, EventArgs e)
    {
        var websiteUrl = _viewModel.SelectedMarker?.WebsiteUrl?.Trim();
        if (string.IsNullOrWhiteSpace(websiteUrl))
        {
            return;
        }

        try
        {
            if (!Uri.TryCreate(websiteUrl, UriKind.Absolute, out var uri))
            {
                uri = new Uri($"https://{websiteUrl}");
            }

            await Launcher.Default.OpenAsync(uri);
        }
        catch (Exception ex)
        {
            SetMapStatus($"Impossibile aprire il sito: {ex.Message}", true);
        }
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
                    _sheetSnapState = VenueSheetSnapState.Teaser;
                    _viewModel.ClearSelection();
                    return;
                }

                await AnimateVenueSheetToAsync(target, true);
                UpdateSheetSnapState(target);
                break;
        }
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (!_viewModel.HasSelectedMarker)
        {
            VenueSheet.TranslationY = GetHiddenSheetOffset();
        }

        _lastOverlayViewport = null;
        RenderViewportOverlay();
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainMapViewModel.SelectedMarker) or nameof(MainMapViewModel.HasSelectedMarker))
        {
            await HidePresenceOverlayAsync(animated: false);
            await HideProfileOverlayAsync(animated: false);
            await HideVenueDetailOverlayAsync(animated: false);
            ClearAreaSelection();
            RenderSelectedPresencePreview();
            RenderViewportOverlay();
            await SyncVenueSheetAsync(animated: true);
        }
    }

    private async void OnOpenVenueDetailsClicked(object? sender, EventArgs e)
    {
        if (_viewModel.SelectedMarker is null)
        {
            return;
        }

        await ShowVenueDetailOverlayAsync(_viewModel.SelectedMarker);
    }

    private void OnVenueDetailOverlayBackdropTapped(object? sender, TappedEventArgs e)
    {
        _ = HideVenueDetailOverlayAsync(animated: true);
    }

    private void OnVenueDetailOverlayCloseClicked(object? sender, EventArgs e)
    {
        _ = HideVenueDetailOverlayAsync(animated: true);
    }

    private void OnNativeMapPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(Microsoft.Maui.Controls.Maps.Map.VisibleRegion) and not "VisibleRegion")
        {
            return;
        }

        _lastOverlayViewport = null;
        RenderViewportOverlay();

        if (DateTimeOffset.UtcNow < _suspendViewportRefreshUntilUtc)
        {
            return;
        }

        ScheduleViewportRefresh();
    }

    private void OnSheetPresenceTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel.SelectedMarker is null || _viewModel.SelectedMarker.PresencePreview.Count == 0)
        {
            return;
        }

        var cluster = CreateSingleMarkerCluster(_viewModel.SelectedMarker, 0.5d, 0.5d, 56d);
        _ = ShowPresenceOverlayAsync(cluster);
    }

    private void OnPresenceOverlayBackdropTapped(object? sender, TappedEventArgs e)
    {
        _ = HidePresenceOverlayAsync(animated: true);
    }

    private void OnPresenceOverlayCloseClicked(object? sender, EventArgs e)
    {
        _ = HidePresenceOverlayAsync(animated: true);
    }

    private void OnAreaDismissClicked(object? sender, EventArgs e)
    {
        ClearAreaSelection();
        RenderViewportOverlay();
    }

    private async void OnAreaPeopleClicked(object? sender, EventArgs e)
    {
        if (_activeAreaCluster is null)
        {
            return;
        }

        await ShowPresenceOverlayAsync(_activeAreaCluster);
    }

    private void OnAreaExploreClicked(object? sender, EventArgs e)
    {
        if (_activeAreaCluster is null)
        {
            return;
        }

        MoveToMarkers(_activeAreaCluster.Markers, suppressViewportRefresh: true);
    }

    private void OnProfileOverlayBackdropTapped(object? sender, TappedEventArgs e)
    {
        _ = HideProfileOverlayAsync(animated: true);
    }

    private void OnProfileOverlayCloseClicked(object? sender, EventArgs e)
    {
        _ = HideProfileOverlayAsync(animated: true);
    }

    private void OnSocialOverlayBackdropTapped(object? sender, TappedEventArgs e)
    {
        _ = HideSocialOverlayAsync(animated: true);
    }

    private void OnSocialOverlayCloseClicked(object? sender, EventArgs e)
    {
        _ = HideSocialOverlayAsync(animated: true);
    }

    private async void OnExitCheckInClicked(object? sender, EventArgs e)
    {
        if (_isSocialBusy)
        {
            return;
        }

        try
        {
            _isSocialBusy = true;
            await _apiClient.ExitActiveCheckInAsync();
            SetSocialStatus("Check-in terminato.", false);
            await RefreshSocialOverlayAsync();
            await RefreshForCurrentViewportAsync(force: true, centerOnMarkers: false);
        }
        catch (Exception ex)
        {
            SetSocialStatus(_apiClient.DescribeException(ex), true);
        }
        finally
        {
            _isSocialBusy = false;
        }
    }

    private async void OnGhostModeToggled(object? sender, ToggledEventArgs e)
    {
        if (_isApplyingSocialState)
        {
            return;
        }

        await UpdatePrivacyAsync(e.Value, null, null);
    }

    private async void OnSharePresenceToggled(object? sender, ToggledEventArgs e)
    {
        if (_isApplyingSocialState)
        {
            return;
        }

        await UpdatePrivacyAsync(null, e.Value, null);
    }

    private async void OnShareIntentionsToggled(object? sender, ToggledEventArgs e)
    {
        if (_isApplyingSocialState)
        {
            return;
        }

        await UpdatePrivacyAsync(null, null, e.Value);
    }

    private void OnTableOverlayBackdropTapped(object? sender, TappedEventArgs e)
    {
        _ = HideTableOverlayAsync(animated: true);
    }

    private void OnTableOverlayCloseClicked(object? sender, EventArgs e)
    {
        _ = HideTableOverlayAsync(animated: true);
    }

    private async void OnSendTableMessageClicked(object? sender, EventArgs e)
    {
        if (_activeTableSummary is null || _isTableBusy)
        {
            return;
        }

        var message = TableMessageEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            SetTableStatus("Scrivi un messaggio.", true);
            return;
        }

        try
        {
            _isTableBusy = true;
            TableLoadingIndicator.IsVisible = true;
            TableLoadingIndicator.IsRunning = true;
            await _apiClient.SendTableMessageAsync(_activeTableSummary.TableId, message);
            TableMessageEntry.Text = string.Empty;
            SetTableStatus("Messaggio inviato.", false);
            await RefreshActiveTableThreadAsync();
        }
        catch (Exception ex)
        {
            SetTableStatus(_apiClient.DescribeException(ex), true);
        }
        finally
        {
            _isTableBusy = false;
            TableLoadingIndicator.IsVisible = false;
            TableLoadingIndicator.IsRunning = false;
        }
    }

    private async void OnProfilePrimaryActionClicked(object? sender, EventArgs e)
    {
        if (_activeProfilePreview is null || _isProfileActionBusy)
        {
            return;
        }

        try
        {
            _isProfileActionBusy = true;
            ProfileActionMessageLabel.IsVisible = false;

            var relationshipStatus = _activeProfile?.RelationshipStatus ?? "none";
            if (relationshipStatus == "pending_received")
            {
                await _apiClient.AcceptFriendRequestAsync(_activeProfilePreview.UserId);
                await RefreshActiveProfileAsync("Amicizia confermata.", false);
                return;
            }

            if (relationshipStatus is "friend" or "pending_sent" or "self")
            {
                return;
            }

            await _apiClient.SendFriendRequestAsync(_activeProfilePreview.UserId);
            await RefreshActiveProfileAsync("Richiesta di amicizia inviata.", false);
        }
        catch (Exception ex)
        {
            SetProfileActionMessage(_apiClient.DescribeException(ex), true);
        }
        finally
        {
            _isProfileActionBusy = false;
            UpdateProfileActionState(_activeProfile, _activeProfilePreview);
        }
    }

    private async void OnProfileInviteClicked(object? sender, EventArgs e)
    {
        if (_activeProfilePreview is null || _isProfileActionBusy)
        {
            return;
        }

        try
        {
            _isProfileActionBusy = true;
            ProfileActionMessageLabel.IsVisible = false;
            await _apiClient.InviteUserToHostedTableAsync(_activeProfilePreview.UserId);
            SetProfileActionMessage("Invito al tavolo inviato.", false);
        }
        catch (Exception ex)
        {
            SetProfileActionMessage(_apiClient.DescribeException(ex), true);
        }
        finally
        {
            _isProfileActionBusy = false;
            UpdateProfileActionState(_activeProfile, _activeProfilePreview);
        }
    }

    private async Task RefreshForCurrentViewportAsync(bool force, bool centerOnMarkers)
    {
        if (_viewModel.IsBusy)
        {
            if (!force)
            {
                ScheduleViewportRefresh();
            }

            return;
        }

        var viewport = GetCurrentViewportOrDefault().Expand(0.18d).Normalize();
        if (!force && _lastRequestedViewport is MapViewport lastViewport && !viewport.IsMeaningfullyDifferentFrom(lastViewport))
        {
            RenderViewportOverlay();
            return;
        }

        _lastRequestedViewport = viewport;
        _shouldAutoFocusOnNextRender = centerOnMarkers;
        await _viewModel.RefreshAsync(viewport);
        _mapLayerLastRefreshUtc = DateTimeOffset.UtcNow;
    }

    private void ApplyDiscoveryFilters(string searchQuery, string category, bool openNowOnly, double? maxDistanceKm)
    {
        _viewModel.ApplyDiscoveryFilters(searchQuery, category, openNowOnly, maxDistanceKm);
        UpdateDiscoveryFilterVisualState();
        ScheduleDiscoveryRefresh();
    }

    private async Task ShowQuickActionRailAsync()
    {
        if (_isQuickActionRailOpen)
        {
            return;
        }

        QuickActionRail.IsVisible = true;
        await QuickActionRail.FadeTo(1, 140, Easing.CubicOut);
        await QuickActionRail.TranslateTo(0, 0, 180, Easing.CubicOut);
        _isQuickActionRailOpen = true;
        QuickMenuButton.BackgroundColor = Color.FromArgb("#E8F0FF");
    }

    private async Task HideQuickActionRailAsync(bool animated)
    {
        if (!_isQuickActionRailOpen && !QuickActionRail.IsVisible)
        {
            return;
        }

        if (animated)
        {
            await Task.WhenAll(
                QuickActionRail.FadeTo(0, 120, Easing.CubicIn),
                QuickActionRail.TranslateTo(28, 0, 140, Easing.CubicIn));
        }
        else
        {
            QuickActionRail.Opacity = 0;
            QuickActionRail.TranslationX = 28;
        }

        QuickActionRail.IsVisible = false;
        _isQuickActionRailOpen = false;
        QuickMenuButton.BackgroundColor = Colors.Transparent;
    }

    private async Task ShowLegendPanelAsync()
    {
        if (_isLegendPanelOpen)
        {
            return;
        }

        LegendPanel.IsVisible = true;
        LegendPanel.TranslationY = -6;
        await Task.WhenAll(
            LegendPanel.FadeTo(1, 160, Easing.CubicOut),
            LegendPanel.TranslateTo(0, 0, 160, Easing.CubicOut));
        _isLegendPanelOpen = true;
    }

    private async Task HideLegendPanelAsync(bool animated)
    {
        if (!_isLegendPanelOpen && !LegendPanel.IsVisible)
        {
            return;
        }

        if (animated)
        {
            await Task.WhenAll(
                LegendPanel.FadeTo(0, 120, Easing.CubicIn),
                LegendPanel.TranslateTo(0, -6, 120, Easing.CubicIn));
        }
        else
        {
            LegendPanel.Opacity = 0;
            LegendPanel.TranslationY = -6;
        }

        LegendPanel.IsVisible = false;
        _isLegendPanelOpen = false;
    }

    private void UpdateDiscoveryFilterVisualState()
    {
        SetFilterButtonState(FilterAllButton, _viewModel.SelectedCategory == "all");
        SetFilterButtonState(FilterBarsButton, _viewModel.SelectedCategory == "bar");
        SetFilterButtonState(FilterFoodButton, _viewModel.SelectedCategory == "food");
        SetFilterButtonState(FilterCafeButton, _viewModel.SelectedCategory == "cafe");
        SetFilterButtonState(FilterNightlifeButton, _viewModel.SelectedCategory == "nightlife");
        SetFilterButtonState(FilterOpenNowButton, _viewModel.OpenNowOnly);
        SetFilterButtonState(FilterDistanceButton, _viewModel.MaxDistanceKm is not null);
        FilterDistanceButton.Text = FormatDistanceFilter(_viewModel.MaxDistanceKm);
    }

    private static void SetFilterButtonState(Button button, bool isActive)
    {
        button.BackgroundColor = isActive ? Color.FromArgb("#2563EB") : Color.FromArgb("#EAF0F7");
        button.TextColor = isActive ? Colors.White : Color.FromArgb("#0F172A");
    }

    private void ScheduleDiscoveryRefresh()
    {
        CancelPendingDiscoveryRefresh();
        _discoveryRefreshCts = new CancellationTokenSource();
        var token = _discoveryRefreshCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(260, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    _lastRequestedViewport = null;
                    await RefreshForCurrentViewportAsync(force: true, centerOnMarkers: false);
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private void CancelPendingDiscoveryRefresh()
    {
        if (_discoveryRefreshCts is null)
        {
            return;
        }

        _discoveryRefreshCts.Cancel();
        _discoveryRefreshCts.Dispose();
        _discoveryRefreshCts = null;
    }

    private async Task UpdatePrivacyAsync(bool? ghostMode, bool? sharePresence, bool? shareIntentions)
    {
        if (_isSocialBusy)
        {
            return;
        }

        try
        {
            _isSocialBusy = true;
            await _apiClient.UpdatePrivacySettingsAsync(ghostMode, sharePresence, shareIntentions);
            SetSocialStatus("Privacy aggiornata.", false);
            await RefreshSocialOverlayAsync();
            await RefreshForCurrentViewportAsync(force: true, centerOnMarkers: false);
        }
        catch (Exception ex)
        {
            SetSocialStatus(_apiClient.DescribeException(ex), true);
            await RefreshSocialOverlayAsync();
        }
        finally
        {
            _isSocialBusy = false;
        }
    }

    private static double? GetNextDistanceFilter(double? currentDistanceKm)
    {
        if (currentDistanceKm is null)
        {
            return 2d;
        }

        if (currentDistanceKm < 3d)
        {
            return 5d;
        }

        if (currentDistanceKm < 8d)
        {
            return 10d;
        }

        return null;
    }

    private static string FormatDistanceFilter(double? distanceKm)
    {
        return distanceKm is > 0 ? $"{distanceKm.Value:0} km" : "Ovunque";
    }

    private void RenderMap()
    {
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
        if (NativeMap.VisibleRegion is not null)
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

            if (visibleMarkers.Count > 0 && _viewModel.Areas.Count > 0)
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

    private void ApplyMapMood()
    {
        if (_isContrastModeEnabled)
        {
            MapMoodOverlay.BackgroundColor = Color.FromArgb("#8A0F172A");
            QuickModeLabel.Text = "Giorno";
            LegendModeLabel.Text = "Modalità notte";
        }
        else
        {
            MapMoodOverlay.BackgroundColor = Color.FromArgb("#2CEEF4FF");
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

    private async Task ShowPresenceOverlayAsync(VenueOverlayCluster cluster)
    {
        if (cluster.PresencePreview.Count == 0)
        {
            return;
        }

        _activePresenceCluster = cluster;
        _activeAreaCluster = cluster.IsCluster ? cluster : _activeAreaCluster;
        PresenceTitleLabel.Text = cluster.IsCluster
            ? $"{cluster.AreaLabel}"
            : $"Presenti a {cluster.Markers[0].Name}";
        PresenceSubtitleLabel.Text = cluster.IsCluster
            ? $"{cluster.PeopleCount} persone stimate • {cluster.VenueCount} luoghi"
            : "Persone che vedi ora per questo locale";

        PresenceListStack.Children.Clear();
        foreach (var preview in cluster.PresencePreview)
        {
            PresenceListStack.Children.Add(CreatePresenceListRow(preview));
        }

        PresenceOverlay.IsVisible = true;
        PresenceSheet.TranslationY = 420;
        await PresenceSheet.TranslateTo(0, 0, 220, Easing.CubicOut);
    }

    private async Task HidePresenceOverlayAsync(bool animated)
    {
        if (!PresenceOverlay.IsVisible)
        {
            return;
        }

        _activePresenceCluster = null;
        if (animated)
        {
            await PresenceSheet.TranslateTo(0, 420, 180, Easing.CubicIn);
        }
        else
        {
            PresenceSheet.TranslationY = 420;
        }

        PresenceOverlay.IsVisible = false;
        PresenceListStack.Children.Clear();
    }

    private void PopulateVenueDetailOverlay(VenueDetails? details, VenueMarker marker)
    {
        VenueDetailNameLabel.Text = marker.Name;
        VenueDetailCategoryPillLabel.Text = FormatVenueCategory(marker.Category);
        VenueDetailAddressLabel.Text = $"{marker.AddressLine}, {marker.City}";
        VenueDetailDescriptionLabel.Text = details?.Description ?? marker.Description ?? string.Empty;
        VenueDetailDescriptionLabel.IsVisible = !string.IsNullOrWhiteSpace(VenueDetailDescriptionLabel.Text);
        VenueDetailPeopleLabel.Text = marker.PeopleEstimate.ToString();
        VenueDetailDensityLabel.Text = FormatDensityLabel(marker.DensityLevel);
        VenueDetailTablesLabel.Text = marker.OpenTables.ToString();
        VenueDetailHoursLabel.Text = details?.HoursSummary ?? marker.HoursSummary ?? string.Empty;
        VenueDetailHoursLabel.IsVisible = !string.IsNullOrWhiteSpace(VenueDetailHoursLabel.Text);
        VenueDetailPhoneLabel.Text = details?.PhoneNumber ?? marker.PhoneNumber ?? string.Empty;
        VenueDetailPhoneLabel.IsVisible = !string.IsNullOrWhiteSpace(VenueDetailPhoneLabel.Text);
        VenueDetailWebsiteLabel.Text = details?.WebsiteUrl ?? marker.WebsiteUrl ?? string.Empty;
        VenueDetailWebsiteLabel.IsVisible = !string.IsNullOrWhiteSpace(VenueDetailWebsiteLabel.Text);
        VenueDetailCoverImage.Source = ResolveVenueCoverSource(details?.CoverImageUrl ?? marker.CoverImageUrl, marker);
        RenderVenueTags(details?.Tags ?? marker.Tags);
    }

    private void RenderVenueTags(IEnumerable<string> tags)
    {
        VenueDetailTagsLayout.Children.Clear();
        var distinctTags = tags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        VenueDetailTagsLayout.IsVisible = distinctTags.Count > 0;
        foreach (var tag in distinctTags)
        {
            VenueDetailTagsLayout.Children.Add(new Border
            {
                Padding = new Thickness(10, 6),
                Margin = new Thickness(0, 0, 8, 8),
                BackgroundColor = Color.FromArgb("#EEF4FF"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Content = new Label
                {
                    Text = tag.Trim(),
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#1D4ED8")
                }
            });
        }
    }

    private static string FormatVenueCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "Locale";
        }

        return category.Trim().ToLowerInvariant() switch
        {
            "cocktail-bar" => "Cocktail bar",
            "pizza-bar" => "Pizza bar",
            _ => category.Trim()
        };
    }

    private static string FormatDensityLabel(string densityLevel)
    {
        return densityLevel?.Trim().ToLowerInvariant() switch
        {
            "very_low" => "Molto bassa",
            "low" => "Bassa",
            "medium" => "Media",
            "high" => "Alta",
            "full" => "Quasi pieno",
            _ => "Stimata"
        };
    }

    private static ImageSource ResolveVenueCoverSource(string? source, VenueMarker marker)
    {
        if (!string.IsNullOrWhiteSpace(source))
        {
            return ImageSource.FromUri(new Uri(source));
        }

        var seed = Uri.EscapeDataString($"{marker.Name}-{marker.Category}-{marker.City}".ToLowerInvariant());
        return ImageSource.FromUri(new Uri($"https://picsum.photos/seed/{seed}/960/640"));
    }

    private View CreatePresenceListRow(PresencePreview preview)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12
        };

        var avatar = CreateAvatarBadge(preview, 42);
        var labels = new VerticalStackLayout
        {
            Spacing = 1,
            Children =
            {
                new Label
                {
                    Text = string.IsNullOrWhiteSpace(preview.DisplayName) ? preview.Nickname : preview.DisplayName,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#0F172A")
                },
                new Label
                {
                    Text = $"@{preview.Nickname}",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#64748B")
                }
            }
        };

        grid.Children.Add(avatar);
        grid.Children.Add(labels);
        Grid.SetColumn(labels, 1);

        var row = new Border
        {
            Padding = new Thickness(12, 10),
            BackgroundColor = Color.FromArgb("#F8FBFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Content = grid
        };

        row.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await ShowProfileForPreviewAsync(preview))
        });

        return row;
    }

    private async Task ShowVenueDetailOverlayAsync(VenueMarker marker)
    {
        await HideQuickActionRailAsync(animated: false);
        await HidePresenceOverlayAsync(animated: false);
        await HideProfileOverlayAsync(animated: false);
        await HideSocialOverlayAsync(animated: false);
        await HideTableOverlayAsync(animated: false);
        await HideEditProfileOverlayAsync(animated: false);
        await HideDirectMessageOverlayAsync(animated: false);

        VenueDetailOverlay.IsVisible = true;
        VenueDetailSheet.TranslationY = 540;
        PopulateVenueDetailHeader(marker);
        VenueDetailAnalyticsSection.IsVisible = false;
        VenueDetailLoadingIndicator.IsVisible = true;
        VenueDetailLoadingIndicator.IsRunning = true;
        await VenueDetailSheet.TranslateTo(0, 0, 220, Easing.CubicOut);

        try
        {
            var details = await _apiClient.GetVenueDetailsAsync(marker.VenueId);
            PopulateVenueDetailContent(marker, details);
        }
        catch (Exception ex)
        {
            VenueDetailStatusLabel.Text = _apiClient.DescribeException(ex);
            VenueDetailStatusLabel.IsVisible = true;
        }
        finally
        {
            VenueDetailLoadingIndicator.IsVisible = false;
            VenueDetailLoadingIndicator.IsRunning = false;
        }
    }

    private async Task HideVenueDetailOverlayAsync(bool animated)
    {
        if (!VenueDetailOverlay.IsVisible)
        {
            return;
        }

        if (animated)
        {
            await VenueDetailSheet.TranslateTo(0, 540, 180, Easing.CubicIn);
        }
        else
        {
            VenueDetailSheet.TranslationY = 540;
        }

        VenueDetailOverlay.IsVisible = false;
        VenueDetailAnalyticsSection.IsVisible = false;
        VenueDetailStatusLabel.IsVisible = false;
    }

    private async Task HideProfileOverlayAsync(bool animated)
    {
        if (!ProfileOverlay.IsVisible)
        {
            return;
        }

        if (animated)
        {
            await ProfileSheet.TranslateTo(0, 460, 180, Easing.CubicIn);
        }
        else
        {
            ProfileSheet.TranslationY = 460;
        }

        ProfileOverlay.IsVisible = false;
        ProfileAvatarHost.Children.Clear();
        _activeProfilePreview = null;
        _activeProfile = null;
        ProfileActionMessageLabel.IsVisible = false;
    }

    private void PopulateVenueDetailHeader(VenueMarker marker)
    {
        VenueDetailNameLabel.Text = marker.Name;
        VenueDetailAddressLabel.Text = $"{marker.AddressLine}, {marker.City}";
        VenueDetailCategoryPillLabel.Text = marker.Category;
        VenueDetailCoverImage.Source = string.IsNullOrWhiteSpace(marker.CoverImageUrl)
            ? null
            : ImageSource.FromUri(new Uri(marker.CoverImageUrl));
    }

    private void PopulateVenueDetailContent(VenueMarker marker, VenueDetails details)
    {
        VenueDetailDescriptionLabel.Text = details.Description;
        VenueDetailDescriptionLabel.IsVisible = !string.IsNullOrWhiteSpace(details.Description);

        VenueDetailTagsLayout.Children.Clear();
        if (details.Tags.Count > 0)
        {
            foreach (var tag in details.Tags)
            {
                var tagBorder = new Border
                {
                    Padding = new Thickness(8, 4),
                    BackgroundColor = Color.FromArgb("#EEF8FF"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Content = new Label
                    {
                        Text = tag,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#1D4ED8")
                    }
                };
                VenueDetailTagsLayout.Children.Add(tagBorder);
            }
        }
        VenueDetailTagsLayout.IsVisible = details.Tags.Count > 0;

        VenueDetailPeopleLabel.Text = details.PeopleEstimate.ToString();
        VenueDetailDensityLabel.Text = details.DensityLevel switch
        {
            "very_low" => "Molto bassa",
            "low" => "Bassa",
            "medium" => "Media",
            "high" => "Alta",
            "very_high" => "Molto alta",
            _ => "Stimata"
        };
        VenueDetailTablesLabel.Text = details.UpcomingTables.Count.ToString();

        VenueDetailHoursLabel.Text = details.HoursSummary;
        VenueDetailHoursLabel.IsVisible = !string.IsNullOrWhiteSpace(details.HoursSummary);

        VenueDetailPhoneLabel.Text = details.PhoneNumber;
        VenueDetailPhoneLabel.IsVisible = !string.IsNullOrWhiteSpace(details.PhoneNumber);

        VenueDetailWebsiteLabel.Text = string.IsNullOrWhiteSpace(details.WebsiteUrl) ? null : new Uri(details.WebsiteUrl).Host.Replace("www.", "");
        VenueDetailWebsiteLabel.IsVisible = !string.IsNullOrWhiteSpace(details.WebsiteUrl);

        // Analytics
        VenueDetailAnalyticsSection.IsVisible = true;

        // Demographics
        VenueDetailDemographicsSection.IsVisible = details.DemographicDataAvailable;
        if (details.DemographicDataAvailable)
        {
            VenueDetailInsufficientDataLabel.IsVisible = false;
            VenueDetailAgeGrid.IsVisible = true;
            VenueDetailGenderGrid.IsVisible = true;

            var ageDict = details.AgeDistribution as Dictionary<string, object>;
            if (ageDict is not null && ageDict.TryGetValue("18-24", out var age1824Obj) && age1824Obj is JsonElement age1824El && age1824El.TryGetInt32(out var age1824))
                VenueDetailAge1824Label.Text = $"{age1824}%";
            if (ageDict is not null && ageDict.TryGetValue("25-34", out var age2534Obj) && age2534Obj is JsonElement age2534El && age2534El.TryGetInt32(out var age2534))
                VenueDetailAge2534Label.Text = $"{age2534}%";
            if (ageDict is not null && ageDict.TryGetValue("35-44", out var age3544Obj) && age3544Obj is JsonElement age3544El && age3544El.TryGetInt32(out var age3544))
                VenueDetailAge3544Label.Text = $"{age3544}%";
            if (ageDict is not null && ageDict.TryGetValue("45+", out var age45PlusObj) && age45PlusObj is JsonElement age45PlusEl && age45PlusEl.TryGetInt32(out var age45Plus))
                VenueDetailAge45PlusLabel.Text = $"{age45Plus}%";

            var genderDict = details.GenderDistribution as Dictionary<string, object>;
            if (genderDict is not null && genderDict.TryGetValue("male", out var maleObj) && maleObj is JsonElement maleEl && maleEl.TryGetInt32(out var male))
                VenueDetailMaleLabel.Text = $"{male}%";
            if (genderDict is not null && genderDict.TryGetValue("female", out var femaleObj) && femaleObj is JsonElement femaleEl && femaleEl.TryGetInt32(out var female))
                VenueDetailFemaleLabel.Text = $"{female}%";
        }
        else
        {
            VenueDetailInsufficientDataLabel.IsVisible = true;
            VenueDetailAgeGrid.IsVisible = false;
            VenueDetailGenderGrid.IsVisible = false;
        }

        // Intentions (feature for future implementation)

        // Trends
        VenueDetailTrendsStack.Children.Clear();
        if (details.AffluenceTrends.Count > 0)
        {
            // Group by hour and show peak times
            var hourlyPeaks = details.AffluenceTrends
                .GroupBy(x => x.BucketStartUtc.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    PeakPeople = g.Max(x => x.PeopleEstimate),
                    AvgPeople = (int)g.Average(x => x.PeopleEstimate),
                    DensityLevels = g.Select(x => x.DensityLevel).Distinct().ToList()
                })
                .OrderBy(x => x.Hour)
                .Take(8)
                .ToList();

            foreach (var peak in hourlyPeaks)
            {
                var densityText = string.Join("/", peak.DensityLevels);
                var trendCard = new Border
                {
                    Padding = new Thickness(12, 8),
                    BackgroundColor = Color.FromArgb("#F8FBFF"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Content = new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children =
                        {
                            new Label
                            {
                                Text = $"{peak.Hour:00}:00 - Picco: {peak.PeakPeople} persone",
                                FontSize = 13,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Color.FromArgb("#0F172A")
                            },
                            new Label
                            {
                                Text = $"Media: {peak.AvgPeople} • Affluenza: {densityText}",
                                FontSize = 11,
                                TextColor = Color.FromArgb("#64748B")
                            }
                        }
                    }
                };
                VenueDetailTrendsStack.Children.Add(trendCard);
            }
        }
        VenueDetailTrendsSection.IsVisible = details.AffluenceTrends.Count > 0;
    }

    private async Task ShowSocialOverlayAsync(bool forceRefresh)
    {
        await HideQuickActionRailAsync(animated: false);
        await HidePresenceOverlayAsync(animated: false);
        await HideProfileOverlayAsync(animated: false);
        await HideVenueDetailOverlayAsync(animated: false);
        await HideTableOverlayAsync(animated: false);

        SocialOverlay.IsVisible = true;
        SocialSheet.TranslationY = 480;
        await SocialSheet.TranslateTo(0, 0, 220, Easing.CubicOut);
        var socialCacheStale = (DateTimeOffset.UtcNow - _socialOverlayLastRefreshUtc) > SocialOverlayCacheDuration;
        if (forceRefresh || _socialHub is null || _socialMeState is null || socialCacheStale)
        {
            await RefreshSocialOverlayAsync();
        }
    }

    private async Task HideSocialOverlayAsync(bool animated)
    {
        if (!SocialOverlay.IsVisible)
        {
            return;
        }

        if (animated)
        {
            await SocialSheet.TranslateTo(0, 480, 180, Easing.CubicIn);
        }
        else
        {
            SocialSheet.TranslationY = 480;
        }

        SocialOverlay.IsVisible = false;
        SocialStatusLabel.IsVisible = false;
    }

    private async Task RefreshSocialOverlayAsync()
    {
        if (_isSocialLoading)
        {
            return;
        }

        try
        {
            _isSocialLoading = true;
            SocialLoadingIndicator.IsVisible = true;
            SocialLoadingIndicator.IsRunning = true;
            SocialStatusLabel.IsVisible = false;

            var hubTask = _apiClient.GetSocialHubAsync();
            var meStateTask = _apiClient.GetSocialMeStateAsync();
            var tablesTask = _apiClient.GetMyTablesAsync();
            var inboxTask = _apiClient.GetDirectMessageInboxAsync();
            await Task.WhenAll(hubTask, meStateTask, tablesTask, inboxTask);

            _socialHub = await hubTask;
            _socialMeState = await meStateTask;
            _socialTables = await tablesTask;
            _messageInbox = await inboxTask;
            _socialOverlayLastRefreshUtc = DateTimeOffset.UtcNow;
            PopulateSocialOverlay(_socialHub, _socialMeState, _socialTables);
        }
        catch (Exception ex)
        {
            SetSocialStatus(_apiClient.DescribeException(ex), true);
        }
        finally
        {
            _isSocialLoading = false;
            SocialLoadingIndicator.IsVisible = false;
            SocialLoadingIndicator.IsRunning = false;
        }
    }

    private void PopulateSocialOverlay(SocialHub hub, SocialMeState meState, IReadOnlyList<SocialTableSummary> tables)
    {
        SocialSummaryLabel.Text = $"{hub.Friends.Count} amici • {_messageInbox.Count} chat • {tables.Count} tavoli";
        PopulateSocialStateCard(meState);
        PopulateDirectMessageSection(_messageInbox);
        PopulateSocialTablesSection(tables);
        PopulateSocialSection(
            SocialInviteHeaderLabel,
            SocialInviteStack,
            hub.TableInvites.Count,
            "Inviti tavolo",
            hub.TableInvites.Select(CreateTableInviteCard).ToList(),
            "Nessun invito tavolo");
        PopulateSocialSection(
            SocialIncomingHeaderLabel,
            SocialIncomingStack,
            hub.IncomingRequests.Count,
            "Richieste in arrivo",
            hub.IncomingRequests.Select(x => CreateSocialConnectionCard(x, "incoming")).ToList(),
            "Nessuna richiesta in arrivo");
        PopulateSocialSection(
            SocialOutgoingHeaderLabel,
            SocialOutgoingStack,
            hub.OutgoingRequests.Count,
            "Richieste inviate",
            hub.OutgoingRequests.Select(x => CreateSocialConnectionCard(x, "outgoing")).ToList(),
            "Nessuna richiesta inviata");
        PopulateSocialSection(
            SocialFriendsHeaderLabel,
            SocialFriendsStack,
            hub.Friends.Count,
            "Amici",
            hub.Friends.Select(x => CreateSocialConnectionCard(x, "friend")).ToList(),
            "Ancora nessun amico");
        PopulateUserSearchSection(SocialUserSearchEntry.Text, _userSearchResults);
    }

    private void PopulateSocialStateCard(SocialMeState meState)
    {
        SocialPresenceStateLabel.Text = meState.ActiveCheckInVenueName switch
        {
            not null => $"Ora sei a {meState.ActiveCheckInVenueName}",
            null when !string.IsNullOrWhiteSpace(meState.ActiveIntentionVenueName) => $"Stai andando a {meState.ActiveIntentionVenueName}",
            _ => "Nessuna presenza live"
        };

        ExitCheckInButton.IsVisible = meState.ActiveCheckInVenueId is not null;

        _isApplyingSocialState = true;
        try
        {
            GhostModeSwitch.IsToggled = meState.IsGhostModeEnabled;
            SharePresenceSwitch.IsToggled = meState.SharePresenceWithFriends;
            ShareIntentionsSwitch.IsToggled = meState.ShareIntentionsWithFriends;
        }
        finally
        {
            _isApplyingSocialState = false;
        }
    }

    private void PopulateSocialTablesSection(IReadOnlyList<SocialTableSummary> tables)
    {
        SocialTablesHeaderLabel.Text = $"I tuoi tavoli ({tables.Count})";
        SocialTablesStack.Children.Clear();

        if (tables.Count == 0)
        {
            SocialTablesStack.Children.Add(CreateSocialPlaceholder("Nessun tavolo attivo."));
            return;
        }

        foreach (var table in tables)
        {
            SocialTablesStack.Children.Add(CreateSocialTableCard(table));
        }
    }

    private void PopulateSocialSection(Label header, VerticalStackLayout container, int count, string title, IReadOnlyList<View> views, string emptyText)
    {
        header.Text = $"{title} ({count})";
        container.Children.Clear();
        if (views.Count == 0)
        {
            container.Children.Add(CreateSocialPlaceholder(emptyText));
            return;
        }

        foreach (var view in views)
        {
            container.Children.Add(view);
        }
    }

    private View CreateSocialPlaceholder(string text)
    {
        return new Border
        {
            Padding = new Thickness(12, 10),
            BackgroundColor = Color.FromArgb("#F8FBFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Content = new Label
            {
                Text = text,
                FontSize = 12,
                TextColor = Color.FromArgb("#64748B")
            }
        };
    }

    private View CreateSocialConnectionCard(SocialConnection connection, string mode)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var preview = new PresencePreview
        {
            UserId = connection.UserId,
            Nickname = connection.Nickname,
            DisplayName = string.IsNullOrWhiteSpace(connection.DisplayName) ? connection.Nickname : connection.DisplayName!,
            AvatarUrl = connection.AvatarUrl
        };

        grid.Children.Add(CreateAvatarBadge(preview, 44));

        var labelStack = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = string.IsNullOrWhiteSpace(connection.DisplayName) ? connection.Nickname : connection.DisplayName,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#0F172A")
                },
                new Label
                {
                    Text = $"@{connection.Nickname}",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#64748B")
                },
                new Label
                {
                    Text = connection.StatusLabel,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#475569")
                }
            }
        };
        grid.Children.Add(labelStack);
        Grid.SetColumn(labelStack, 1);

        var actionHost = new HorizontalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center
        };

        switch (mode)
        {
            case "incoming":
                actionHost.Children.Add(CreateSocialActionButton("Accetta", true, async () =>
                {
                    await _apiClient.AcceptFriendRequestAsync(connection.UserId);
                    SetSocialStatus("Richiesta accettata.", false);
                    await RefreshSocialOverlayAsync();
                }));
                actionHost.Children.Add(CreateSocialActionButton("Rifiuta", false, async () =>
                {
                    await _apiClient.RejectFriendRequestAsync(connection.UserId);
                    SetSocialStatus("Richiesta rimossa.", false);
                    await RefreshSocialOverlayAsync();
                }));
                break;
            case "outgoing":
                actionHost.Children.Add(CreateSocialActionButton("Annulla", false, async () =>
                {
                    await _apiClient.RejectFriendRequestAsync(connection.UserId);
                    SetSocialStatus("Richiesta annullata.", false);
                    await RefreshSocialOverlayAsync();
                }));
                break;
            default:
                actionHost.Children.Add(CreateSocialActionButton("Profilo", false, async () =>
                {
                    await OpenProfileFromSocialAsync(preview);
                }));
                break;
        }

        grid.Children.Add(actionHost);
        Grid.SetColumn(actionHost, 2);

        var card = new Border
        {
            Padding = new Thickness(12, 10),
            BackgroundColor = Color.FromArgb("#F8FBFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Content = grid
        };

        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenProfileFromSocialAsync(preview))
        });

        return card;
    }

    private View CreateTableInviteCard(SocialTableInvite invite)
    {
        var preview = new PresencePreview
        {
            UserId = invite.HostUserId,
            Nickname = invite.HostNickname,
            DisplayName = string.IsNullOrWhiteSpace(invite.HostDisplayName) ? invite.HostNickname : invite.HostDisplayName!,
            AvatarUrl = invite.HostAvatarUrl
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        grid.Children.Add(CreateAvatarBadge(preview, 44));

        var labels = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = invite.Title,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#0F172A")
                },
                new Label
                {
                    Text = $"{invite.VenueName} • {invite.VenueCategory}",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#64748B")
                },
                new Label
                {
                    Text = $"Host: {(string.IsNullOrWhiteSpace(invite.HostDisplayName) ? invite.HostNickname : invite.HostDisplayName)} • {invite.StartsAtUtc.ToLocalTime():ddd HH:mm}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#475569")
                }
            }
        };
        grid.Children.Add(labels);
        Grid.SetColumn(labels, 1);

        var action = CreateSocialActionButton("Accetta", true, async () =>
        {
            await _apiClient.AcceptTableInviteAsync(invite.TableId);
            SetSocialStatus("Invito accettato.", false);
            await RefreshSocialOverlayAsync();
            await RefreshForCurrentViewportAsync(force: true, centerOnMarkers: false);
        });
        grid.Children.Add(action);
        Grid.SetColumn(action, 2);

        var card = new Border
        {
            Padding = new Thickness(12, 10),
            BackgroundColor = Color.FromArgb("#F8FBFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Content = grid
        };

        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenProfileFromSocialAsync(preview))
        });

        return card;
    }

    private View CreateSocialTableCard(SocialTableSummary table)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var labels = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                new Label
                {
                    Text = table.Title,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#0F172A")
                },
                new Label
                {
                    Text = $"{table.VenueName} • {table.VenueCategory}",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#64748B")
                },
                new Label
                {
                    Text = $"{table.StartsAtUtc.ToLocalTime():ddd HH:mm} • {BuildTableMembershipLabel(table)}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#475569")
                },
                new Label
                {
                    Text = $"{table.AcceptedCount}/{table.Capacity} dentro • {table.RequestedCount} richieste • {table.InvitedCount} inviti",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#475569")
                }
            }
        };

        grid.Children.Add(labels);

        var action = CreateSocialActionButton("Apri", true, async () =>
        {
            await ShowTableOverlayAsync(table);
        });
        grid.Children.Add(action);
        Grid.SetColumn(action, 1);

        var card = new Border
        {
            Padding = new Thickness(12, 10),
            BackgroundColor = Color.FromArgb("#F8FBFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Content = grid
        };

        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await ShowTableOverlayAsync(table))
        });

        return card;
    }

    private View CreateSocialActionButton(string text, bool primary, Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 12,
            MinimumHeightRequest = 34,
            Padding = new Thickness(10, 8),
            CornerRadius = 14
        };

        if (primary)
        {
            button.BackgroundColor = Color.FromArgb("#2563EB");
            button.TextColor = Colors.White;
        }
        else
        {
            button.BackgroundColor = Color.FromArgb("#EAF0F7");
            button.TextColor = Color.FromArgb("#0F172A");
        }

        button.Clicked += async (_, _) =>
        {
            if (_isSocialBusy)
            {
                return;
            }

            try
            {
                _isSocialBusy = true;
                await action();
            }
            catch (Exception ex)
            {
                SetSocialStatus(_apiClient.DescribeException(ex), true);
            }
            finally
            {
                _isSocialBusy = false;
            }
        };

        return button;
    }

    private async Task ShowTableOverlayAsync(SocialTableSummary table)
    {
        await HideQuickActionRailAsync(animated: false);
        await HidePresenceOverlayAsync(animated: false);
        await HideProfileOverlayAsync(animated: false);
        await HideVenueDetailOverlayAsync(animated: false);

        _activeTableSummary = table;
        _activeTableThread = null;
        TableTitleLabel.Text = table.Title;
        TableMetaLabel.Text = $"{table.VenueName} • {table.VenueCategory} • {table.StartsAtUtc.ToLocalTime():ddd HH:mm}";
        TableStatusLabel.IsVisible = false;
        TableRequestsStack.Children.Clear();
        TableMessagesStack.Children.Clear();
        TableLoadingIndicator.IsVisible = true;
        TableLoadingIndicator.IsRunning = true;
        TableOverlay.IsVisible = true;
        TableSheet.TranslationY = 500;
        await TableSheet.TranslateTo(0, 0, 220, Easing.CubicOut);
        await RefreshActiveTableThreadAsync();
    }

    private async Task HideTableOverlayAsync(bool animated)
    {
        if (!TableOverlay.IsVisible)
        {
            return;
        }

        if (animated)
        {
            await TableSheet.TranslateTo(0, 500, 180, Easing.CubicIn);
        }
        else
        {
            TableSheet.TranslationY = 500;
        }

        TableOverlay.IsVisible = false;
        TableRequestsStack.Children.Clear();
        TableMessagesStack.Children.Clear();
        TableStatusLabel.IsVisible = false;
        TableMessageEntry.Text = string.Empty;
        _activeTableSummary = null;
        _activeTableThread = null;
    }

    private async Task RefreshActiveTableThreadAsync()
    {
        if (_activeTableSummary is null)
        {
            return;
        }

        try
        {
            TableLoadingIndicator.IsVisible = true;
            TableLoadingIndicator.IsRunning = true;
            var thread = await _apiClient.GetTableThreadAsync(_activeTableSummary.TableId);
            _activeTableThread = thread;
            _activeTableSummary = thread.Table;
            PopulateTableOverlay(thread);
        }
        catch (Exception ex)
        {
            SetTableStatus(_apiClient.DescribeException(ex), true);
        }
        finally
        {
            TableLoadingIndicator.IsVisible = false;
            TableLoadingIndicator.IsRunning = false;
        }
    }

    private void PopulateTableOverlay(SocialTableThread thread)
    {
        TableTitleLabel.Text = thread.Table.Title;
        TableMetaLabel.Text = $"{thread.Table.VenueName} • {thread.Table.VenueCategory} • {thread.Table.StartsAtUtc.ToLocalTime():ddd HH:mm}";
        TableRequestsHeaderLabel.Text = thread.Table.IsHost
            ? $"Richieste ({thread.Requests.Count})"
            : $"Persone in attesa ({thread.Requests.Count})";

        TableRequestsStack.Children.Clear();
        if (thread.Requests.Count == 0)
        {
            TableRequestsStack.Children.Add(CreateSocialPlaceholder("Nessuna richiesta pendente."));
        }
        else
        {
            foreach (var request in thread.Requests)
            {
                TableRequestsStack.Children.Add(CreateTableRequestCard(thread.Table, request));
            }
        }

        TableMessagesStack.Children.Clear();
        if (thread.Messages.Count == 0)
        {
            TableMessagesStack.Children.Add(CreateSocialPlaceholder("Nessun messaggio ancora."));
        }
        else
        {
            foreach (var message in thread.Messages)
            {
                TableMessagesStack.Children.Add(CreateTableMessageCard(message));
            }
        }
    }

    private View CreateTableRequestCard(SocialTableSummary table, SocialTableRequest request)
    {
        var preview = new PresencePreview
        {
            UserId = request.UserId,
            Nickname = request.Nickname,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Nickname : request.DisplayName!,
            AvatarUrl = request.AvatarUrl
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        grid.Children.Add(CreateAvatarBadge(preview, 40));

        var labels = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Nickname : request.DisplayName,
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#0F172A")
                },
                new Label
                {
                    Text = $"@{request.Nickname} • {FormatParticipantStatus(request.Status)}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#64748B")
                }
            }
        };
        grid.Children.Add(labels);
        Grid.SetColumn(labels, 1);

        var actions = new HorizontalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center
        };

        if (table.IsHost)
        {
            actions.Children.Add(CreateSocialActionButton("Approva", true, async () =>
            {
                await _apiClient.ApproveTableRequestAsync(table.TableId, request.UserId);
                SetTableStatus("Richiesta approvata.", false);
                await RefreshActiveTableThreadAsync();
                await RefreshSocialOverlayAsync();
                await RefreshForCurrentViewportAsync(force: true, centerOnMarkers: false);
            }));
            actions.Children.Add(CreateSocialActionButton("Rifiuta", false, async () =>
            {
                await _apiClient.RejectTableRequestAsync(table.TableId, request.UserId);
                SetTableStatus("Richiesta rimossa.", false);
                await RefreshActiveTableThreadAsync();
                await RefreshSocialOverlayAsync();
            }));
        }

        grid.Children.Add(actions);
        Grid.SetColumn(actions, 2);

        var card = new Border
        {
            Padding = new Thickness(12, 10),
            BackgroundColor = Color.FromArgb("#F8FBFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Content = grid
        };

        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenProfileFromTableAsync(preview))
        });

        return card;
    }

    private View CreateTableMessageCard(SocialTableMessage message)
    {
        var preview = new PresencePreview
        {
            UserId = message.UserId,
            Nickname = message.Nickname,
            DisplayName = string.IsNullOrWhiteSpace(message.DisplayName) ? message.Nickname : message.DisplayName!,
            AvatarUrl = message.AvatarUrl
        };

        var bubble = new Border
        {
            Padding = new Thickness(12, 10),
            BackgroundColor = message.IsMine ? Color.FromArgb("#2563EB") : Color.FromArgb("#F8FBFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            MaximumWidthRequest = 260,
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label
                    {
                        Text = message.IsMine ? "Tu" : (string.IsNullOrWhiteSpace(message.DisplayName) ? message.Nickname : message.DisplayName),
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = message.IsMine ? Colors.White.WithAlpha(0.92f) : Color.FromArgb("#0F172A")
                    },
                    new Label
                    {
                        Text = message.Body,
                        FontSize = 13,
                        TextColor = message.IsMine ? Colors.White : Color.FromArgb("#0F172A")
                    },
                    new Label
                    {
                        Text = message.SentAtUtc.ToLocalTime().ToString("ddd HH:mm"),
                        FontSize = 10,
                        TextColor = message.IsMine ? Colors.White.WithAlpha(0.82f) : Color.FromArgb("#64748B")
                    }
                }
            }
        };

        if (message.IsMine)
        {
            return new HorizontalStackLayout
            {
                HorizontalOptions = LayoutOptions.End,
                Children = { bubble }
            };
        }

        var row = new HorizontalStackLayout
        {
            Spacing = 10,
            HorizontalOptions = LayoutOptions.Start,
            Children =
            {
                CreateAvatarBadge(preview, 34),
                bubble
            }
        };

        row.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenProfileFromTableAsync(preview))
        });

        return row;
    }

    private async Task OpenProfileFromSocialAsync(PresencePreview preview)
    {
        await HideSocialOverlayAsync(animated: false);
        await ShowUserProfileAsync(preview);
    }

    private async Task OpenProfileFromTableAsync(PresencePreview preview)
    {
        await HideTableOverlayAsync(animated: false);
        await ShowUserProfileAsync(preview);
    }

    private void SetSocialStatus(string message, bool isError)
    {
        SocialStatusLabel.Text = message;
        SocialStatusLabel.TextColor = isError
            ? Color.FromArgb("#B91C1C")
            : Color.FromArgb("#1D4ED8");
        SocialStatusLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private void SetTableStatus(string message, bool isError)
    {
        TableStatusLabel.Text = message;
        TableStatusLabel.TextColor = isError
            ? Color.FromArgb("#B91C1C")
            : Color.FromArgb("#1D4ED8");
        TableStatusLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private static string BuildTableMembershipLabel(SocialTableSummary table)
    {
        if (table.IsHost)
        {
            return "Host";
        }

        return table.MembershipStatus switch
        {
            "accepted" => "Dentro",
            "requested" => "In approvazione",
            "invited" => "Invitato",
            _ => "Tavolo"
        };
    }

    private static string FormatParticipantStatus(string status)
    {
        return status switch
        {
            "accepted" => "accettato",
            "requested" => "richiesta",
            "invited" => "invitato",
            _ => status
        };
    }

    private static string BuildProfileMeta(UserProfile? profile)
    {
        if (profile is null)
        {
            return "--";
        }

        var parts = new List<string>();
        if (profile.BirthYear is int birthYear)
        {
            var age = Math.Max(0, DateTime.UtcNow.Year - birthYear);
            if (age > 0)
            {
                parts.Add($"{age} anni");
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.Gender) && !profile.Gender.Equals("undisclosed", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(profile.Gender);
        }

        return parts.Count == 0 ? "Profilo live" : string.Join(" • ", parts);
    }

    private async Task RefreshActiveProfileAsync(string successMessage, bool isError)
    {
        if (_activeProfilePreview is null)
        {
            return;
        }

        _activeProfile = await _apiClient.GetUserProfileAsync(_activeProfilePreview.UserId);
        PopulateProfileOverlay(_activeProfile, _activeProfilePreview);
        SetProfileActionMessage(successMessage, isError);
    }

    private void UpdateProfileActionState(UserProfile? profile, PresencePreview? fallback)
    {
        var targetUserId = profile?.UserId ?? fallback?.UserId ?? Guid.Empty;
        var isSelf = targetUserId != Guid.Empty && _apiClient.CurrentUserId == targetUserId;
        var relationshipStatus = profile?.RelationshipStatus ?? (isSelf ? "self" : "loading");

        ProfilePrimaryActionButton.IsVisible = !isSelf;
        ProfileInviteActionButton.IsVisible = !isSelf && (profile?.CanInviteToTable ?? true);
        ProfileMessageActionButton.IsVisible = !isSelf;
        ProfileBlockActionButton.IsVisible = !isSelf;
        ProfileReportActionButton.IsVisible = !isSelf;

        ProfileInviteActionButton.IsEnabled = !_isProfileActionBusy && !isSelf && profile is not null && (profile?.CanInviteToTable ?? false);
        ProfileMessageActionButton.IsEnabled = !_isProfileActionBusy && !isSelf && (profile?.CanMessageDirectly ?? false);
        ProfileReportActionButton.IsEnabled = !_isProfileActionBusy && !isSelf && profile is not null;

        switch (relationshipStatus)
        {
            case "friend":
                ProfilePrimaryActionButton.Text = "Amici";
                ProfilePrimaryActionButton.IsEnabled = false;
                break;
            case "pending_sent":
                ProfilePrimaryActionButton.Text = "Richiesta inviata";
                ProfilePrimaryActionButton.IsEnabled = false;
                break;
            case "pending_received":
                ProfilePrimaryActionButton.Text = "Accetta";
                ProfilePrimaryActionButton.IsEnabled = !_isProfileActionBusy;
                break;
            case "self":
                ProfilePrimaryActionButton.IsVisible = false;
                ProfilePrimaryActionButton.IsEnabled = false;
                break;
            case "blocked_by_you":
                ProfilePrimaryActionButton.Text = "Bloccato";
                ProfilePrimaryActionButton.IsEnabled = false;
                break;
            case "blocked_you":
                ProfilePrimaryActionButton.Text = "Profilo limitato";
                ProfilePrimaryActionButton.IsEnabled = false;
                break;
            default:
                ProfilePrimaryActionButton.Text = profile is null ? "Caricamento" : "Aggiungi";
                ProfilePrimaryActionButton.IsEnabled = !_isProfileActionBusy && profile is not null;
                break;
        }

        ProfileInviteActionButton.Text = "Invita al tavolo";
        ProfileMessageActionButton.Text = "Chat";
        ProfileBlockActionButton.Text = profile?.IsBlockedByViewer == true ? "Sblocca" : "Blocca";
        ProfileBlockActionButton.IsEnabled = !_isProfileActionBusy && !isSelf && profile is not null;
    }

    private void SetProfileActionMessage(string message, bool isError)
    {
        ProfileActionMessageLabel.Text = message;
        ProfileActionMessageLabel.TextColor = isError
            ? Color.FromArgb("#B91C1C")
            : Color.FromArgb("#1D4ED8");
        ProfileActionMessageLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
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
        var initials = BuildInitials(preview.DisplayName, preview.Nickname);
        var content = new Grid();

        content.Children.Add(new Label
        {
            Text = initials,
            FontSize = size >= 34 ? 12 : 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        });

        if (Uri.TryCreate(preview.AvatarUrl, UriKind.Absolute, out var avatarUri))
        {
            var avatarImage = new Image
            {
                Source = ImageSource.FromUri(avatarUri),
                Aspect = Aspect.AspectFill,
                WidthRequest = size,
                HeightRequest = size,
                InputTransparent = true
            };
            avatarImage.Clip = new EllipseGeometry
            {
                Center = new Point(size / 2d, size / 2d),
                RadiusX = size / 2d,
                RadiusY = size / 2d
            };

            content.Children.Add(avatarImage);
        }

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
            Content = content
        };
    }

    private static string BuildInitials(string displayName, string nickname)
    {
        var source = string.IsNullOrWhiteSpace(displayName)
            ? nickname
            : displayName;

        if (string.IsNullOrWhiteSpace(source))
        {
            return "?";
        }

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
        if (string.IsNullOrWhiteSpace(seed))
        {
            return Color.FromArgb("#2563EB");
        }

        var hash = seed.Aggregate(17, (current, ch) => current * 31 + ch);
        return Color.FromArgb(palette[Math.Abs(hash) % palette.Length]);
    }

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
            return;
        }

        if (!VenueSheet.IsVisible)
        {
            VenueSheet.TranslationY = GetHiddenSheetOffset();
            return;
        }

        await AnimateVenueSheetToAsync(GetHiddenSheetOffset(), animated);
        VenueSheet.IsVisible = false;
        _sheetSnapState = VenueSheetSnapState.Teaser;
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
        if (NativeMap.VisibleRegion is not MapSpan visibleRegion)
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

            RenderViewportOverlay();
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
                    new GradientStop(Color.FromArgb("#5B9BFF").WithAlpha(0.30f), 0f),
                    new GradientStop(Color.FromArgb("#3B82F6").WithAlpha(0.14f), 0.58f),
                    new GradientStop(Color.FromArgb("#2563EB").WithAlpha(0f), 1f)
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
            Stroke = Color.FromArgb("#D5E4FF"),
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
            : Color.FromArgb("#1D4ED8");
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

    private Color ResolveAreaColor(MapArea area)
    {
        if (area.OpenTables > 0)
        {
            return Color.FromArgb("#2563EB");
        }

        if (area.ActiveIntentions > 0)
        {
            return Color.FromArgb("#7C3AED");
        }

        if (area.ActiveCheckIns > 0)
        {
            return Color.FromArgb("#0EA5E9");
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

        return marker.BubbleIntensity switch
        {
            <= 0 => Colors.Transparent,
            < 25 => Color.FromArgb("#DBEAFE"),
            < 45 => Color.FromArgb("#93C5FD"),
            < 65 => Color.FromArgb("#60A5FA"),
            < 85 => Color.FromArgb("#2563EB"),
            _ => Color.FromArgb("#1D4ED8")
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
