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
    private static readonly bool UseLightweightVenueBadges = true;
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
    private readonly AppIntentService _appIntentService;

    private CancellationTokenSource? _viewportRefreshCts;
    private CancellationTokenSource? _overlayRenderCts;
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
    private bool _isContrastModeEnabled = true;
    private bool _isSocialBusy;
    private bool _isSocialLoading;
    private bool _isTableBusy;
    private bool _isApplyingSocialState;
    private bool _permissionsRequested;
    private bool _isProfileActionBusy;
    private bool _shouldAutoFocusOnNextRender = true;
    private bool _isRefreshingCurrentUserLocation;
    private string? _activeInterestFilter;
    private double _sheetPanStartY;
    private bool _isShaking;
    private DateTimeOffset _lastShakeUtc = DateTimeOffset.MinValue;
    private MapViewport? _lastRequestedViewport;
    private MapViewport? _lastOverlayViewport;
    private Location? _currentUserLocation;
    private DateTimeOffset _lastCurrentUserLocationRefreshUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _mapLayerLastRefreshUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _suspendViewportRefreshUntilUtc = DateTimeOffset.MinValue;
    private VenueSheetSnapState _sheetSnapState = VenueSheetSnapState.Teaser;
    private IDispatcherTimer? _overlaySyncTimer;
    private IDispatcherTimer? _chatPollTimer;

    private readonly ChatHubService _chatHub;

    public MainMapPage(MainMapViewModel viewModel, LoginViewModel loginViewModel, IDevicePermissionService permissions, ApiClient apiClient, ChatHubService chatHub, AppIntentService appIntentService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _loginViewModel = loginViewModel;
        _permissions = permissions;
        _apiClient = apiClient;
        _chatHub = chatHub;
        _appIntentService = appIntentService;
        _chatHub.MessageReceived += OnHubMessageReceived;
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
        MessagingCenter.Subscribe<SocialViewModel, Guid>(this, "OpenDirectMessage", async (_, peerId) =>
        {
            await ShowDirectMessageOverlayAsync(peerId);
        });
        MessagingCenter.Subscribe<SocialViewModel, Guid>(this, "OpenTable", async (_, tableId) =>
        {
            await ShowTableOverlayAsync(tableId);
        });
        MessagingCenter.Subscribe<App, string>(this, "DeepLinkVenue", async (_, venueId) =>
        {
            if (Guid.TryParse(venueId, out var id))
            {
                var marker = _viewModel.Markers.FirstOrDefault(m => m.VenueId == id);
                if (marker is not null)
                {
                    _viewModel.SelectMarker(marker);
                }
            }
        });
        MessagingCenter.Subscribe<App, string>(this, "DeepLinkChat", async (_, peerId) =>
        {
            if (Guid.TryParse(peerId, out var id))
            {
                await ShowDirectMessageOverlayAsync(id);
            }
        });
        MessagingCenter.Subscribe<App, string>(this, "DeepLinkTable", async (_, tableId) =>
        {
            if (Guid.TryParse(tableId, out var id))
            {
                await ShowTableOverlayAsync(id);
            }
        });
        MessagingCenter.Subscribe<ProfilePage>(this, "OpenEditProfile", async (_) =>
        {
            await ShowEditProfileOverlayAsync();
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartOverlaySyncTimer();
        StartBumpAccelerometer();
        StartChatPolling();
        _ = _chatHub.ConnectAsync();

        try
        {
            if (!_permissionsRequested)
            {
                _permissionsRequested = true;
                await _viewModel.RegisterStoredDeviceTokenAsync();
            }

            EnsureInitialMapRegion();
            _ = WarmPersonalMapContextAsync();
            await Task.Delay(250);
            var mapCacheStale = (DateTimeOffset.UtcNow - _mapLayerLastRefreshUtc) > MapLayerCacheDuration;
            await RefreshForCurrentViewportAsync(force: mapCacheStale || _viewModel.Markers.Count == 0, centerOnMarkers: true);
            await SyncVenueSheetAsync(animated: false);
            ApplyMapMood();
            await ProcessPendingAppIntentAsync();
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
        StopBumpAccelerometer();
        StopChatPolling();
        CancelPendingViewportRefresh();
        CancelPendingOverlayRender();
        CancelPendingDiscoveryRefresh();
        _chatHub.MessageReceived -= OnHubMessageReceived;
        _ = _chatHub.DisconnectAsync();
        MessagingCenter.Unsubscribe<SocialViewModel, Guid>(this, "OpenDirectMessage");
        MessagingCenter.Unsubscribe<SocialViewModel, Guid>(this, "OpenTable");
        MessagingCenter.Unsubscribe<App, string>(this, "DeepLinkVenue");
        MessagingCenter.Unsubscribe<App, string>(this, "DeepLinkChat");
        MessagingCenter.Unsubscribe<App, string>(this, "DeepLinkTable");
    }

    private async void OnHubMessageReceived(object? sender, HubMessageArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (e.ThreadId == $"dm-{_activeDirectMessageProfile?.UserId}" && DirectMessageOverlay.IsVisible)
            {
                await RefreshDirectMessageThreadAsync();
            }
            else if (e.ThreadId == $"table-{_activeTableSummary?.TableId}" && TableOverlay.IsVisible)
            {
                await RefreshActiveTableThreadAsync();
            }
        });
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
        await Shell.Current.GoToAsync("//login");
    }

    private async void OnBumpFabClicked(object? sender, EventArgs e)
    {
        HapticService.Heavy();
        if (_viewModel.SelectedMarker is null)
        {
            _viewModel.SetStatusMessage("Seleziona un locale sulla mappa per fare check-in!");
            return;
        }

        var action = await DisplayActionSheet(
            "Cosa vuoi fare?",
            "Annulla",
            null,
            "✅ Check-in",
            "💭 Ci vado",
            "🪑 Apri tavolo",
            "⚡ Flare");

        switch (action)
        {
            case "✅ Check-in":
                HapticService.Success();
                _viewModel.CheckInCommand.Execute(null);
                break;
            case "💭 Ci vado":
                HapticService.Success();
                _viewModel.PlanIntentionCommand.Execute(null);
                break;
            case "🪑 Apri tavolo":
                HapticService.Success();
                await ShowCreateTableFormAsync();
                break;
            case "⚡ Flare":
                HapticService.Success();
                await LaunchSelectedVenueFlareAsync();
                break;
        }
    }

    private async Task LaunchSelectedVenueFlareAsync()
    {
        if (_viewModel.SelectedMarker is null)
        {
            _viewModel.SetStatusMessage("Seleziona un locale prima di lanciare un flare.");
            return;
        }

        var marker = _viewModel.SelectedMarker;
        var message = await DisplayPromptAsync(
            "Flare",
            "Che segnale vuoi mandare agli amici?",
            initialValue: $"Passa da {marker.Name}, chi c'è?");

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            await _apiClient.SendFlareAsync(marker.Latitude, marker.Longitude, message.Trim());
            await DisplayAlert("Flare", "Flare inviato agli amici.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Flare", _apiClient.DescribeException(ex), "OK");
        }
    }

    private async Task ProcessPendingAppIntentAsync()
    {
        var pendingIntent = _appIntentService.Consume();
        if (pendingIntent is null)
        {
            return;
        }

        switch (pendingIntent.Type)
        {
            case AppIntentType.Profile:
                await ShowUserProfileAsync(pendingIntent.EntityId);
                break;
            case AppIntentType.DirectMessage:
                await ShowDirectMessageOverlayAsync(pendingIntent.EntityId);
                break;
            case AppIntentType.Table:
                await ShowTableOverlayAsync(pendingIntent.EntityId);
                break;
        }
    }

    private async Task ShowCreateTableFormAsync()
    {
        if (_viewModel.SelectedMarker is null) return;
        await Shell.Current.GoToAsync(
            $"{nameof(CreateTablePage)}" +
            $"?venueId={Uri.EscapeDataString(_viewModel.SelectedMarker.VenueId.ToString())}" +
            $"&venueName={Uri.EscapeDataString(_viewModel.SelectedMarker.Name)}" +
            $"&venueCategory={Uri.EscapeDataString(_viewModel.SelectedMarker.Category)}");
    }

    private async void OnCreateTableClicked(object? sender, EventArgs e)
    {
        await ShowCreateTableFormAsync();
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
        _activeInterestFilter = null;
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

    private async void OnInterestsFilterClicked(object? sender, EventArgs e)
    {
        var interests = _myProfile?.Interests;
        if (interests is null || interests.Count == 0)
        {
            await DisplayAlert("Nessun interesse", "Aggiungi i tuoi interessi nel profilo per usare questo filtro.", "OK");
            return;
        }

        var options = interests.Concat(new[] { "Tutti" }).ToArray();
        var picked = await DisplayActionSheet("Filtra per interesse", "Annulla", null, options);
        if (picked is null || picked == "Annulla")
        {
            return;
        }

        if (picked == "Tutti")
        {
            _activeInterestFilter = null;
            ApplyDiscoveryFilters(string.Empty, _viewModel.SelectedCategory, _viewModel.OpenNowOnly, _viewModel.MaxDistanceKm);
            return;
        }

        _activeInterestFilter = picked;
        ApplyDiscoveryFilters(picked, _viewModel.SelectedCategory, _viewModel.OpenNowOnly, _viewModel.MaxDistanceKm);
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

        if (e.PropertyName == nameof(MainMapViewModel.ActionMessage) && !string.IsNullOrWhiteSpace(_viewModel.ActionMessage))
        {
            if (_viewModel.ActionMessage.Contains("Check-in", StringComparison.OrdinalIgnoreCase))
            {
                await ShowSuccessOverlayAsync("Check-in fatto! ⚡");
            }
            else if (_viewModel.ActionMessage.Contains("Intenzione", StringComparison.OrdinalIgnoreCase))
            {
                await ShowSuccessOverlayAsync("Ci vado! 💭");
            }
            else if (_viewModel.ActionMessage.Contains("Tavolo", StringComparison.OrdinalIgnoreCase))
            {
                await ShowSuccessOverlayAsync("Tavolo aperto! 🪑");
            }
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

    private async void OnVenueDetailVibeClicked(object? sender, EventArgs e)
    {
        if (_viewModel.SelectedMarker is null)
        {
            return;
        }

        var vibe = await DisplayActionSheet(
            "Che vibe c'è adesso?",
            "Annulla",
            null,
            "🔥 Festa",
            "🍸 Chill",
            "🎶 Musica alta",
            "🗣️ Si parla bene",
            "🚶 Coda lunga");

        if (string.IsNullOrWhiteSpace(vibe) || vibe == "Annulla")
        {
            return;
        }

        try
        {
            await _apiClient.SubmitVenueVibeAsync(_viewModel.SelectedMarker.VenueId, vibe);
            await DisplayAlert("Vibe", "Grazie, vibe registrato.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Vibe", _apiClient.DescribeException(ex), "OK");
        }
    }

    private async void OnVenueDetailShareClicked(object? sender, EventArgs e)
    {
        if (_viewModel.SelectedMarker is null)
        {
            return;
        }

        var link = DeepLinkService.BuildUniversalLink(_apiClient.BaseAddress, "venue", _viewModel.SelectedMarker.VenueId);
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Uri = link,
            Title = $"Apri {_viewModel.SelectedMarker.Name} su Cloudy",
            Text = $"{_viewModel.SelectedMarker.Name}\n{link}"
        });
    }

    private void OnNativeMapPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(Microsoft.Maui.Controls.Maps.Map.VisibleRegion) and not "VisibleRegion")
        {
            return;
        }

        _lastOverlayViewport = null;
        ScheduleOverlayRender(TimeSpan.FromMilliseconds(120));

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
            HapticService.Medium();
            _isSocialBusy = true;
            await _apiClient.ExitActiveCheckInAsync();
            HapticService.Success();
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
            HapticService.Light();
            _isTableBusy = true;
            TableLoadingIndicator.IsVisible = true;
            TableLoadingIndicator.IsRunning = true;
            await _apiClient.SendTableMessageAsync(_activeTableSummary.TableId, message);
            TableMessageEntry.Text = string.Empty;
            SetTableStatus("Messaggio inviato.", false);
            HapticService.Success();
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
            HapticService.Medium();
            _isProfileActionBusy = true;
            ProfileActionMessageLabel.IsVisible = false;

            var relationshipStatus = _activeProfile?.RelationshipStatus ?? "none";
            if (relationshipStatus == "pending_received")
            {
                await _apiClient.AcceptFriendRequestAsync(_activeProfilePreview.UserId);
                HapticService.Success();
                await RefreshActiveProfileAsync("Amicizia confermata.", false);
                return;
            }

            if (relationshipStatus is "friend" or "pending_sent" or "self")
            {
                return;
            }

            await _apiClient.SendFriendRequestAsync(_activeProfilePreview.UserId);
            HapticService.Success();
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
            HapticService.Medium();
            _isProfileActionBusy = true;
            ProfileActionMessageLabel.IsVisible = false;
            await _apiClient.InviteUserToHostedTableAsync(_activeProfilePreview.UserId);
            HapticService.Success();
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
        await QuickActionRail.TranslateTo(0, 0, 220, Easing.SpringOut);
        _isQuickActionRailOpen = true;
        QuickMenuButton.BackgroundColor = Color.FromArgb("#F5F3FF");
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
                QuickActionRail.TranslateTo(28, 0, 180, Easing.SpringIn));
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
            LegendPanel.TranslateTo(0, 0, 200, Easing.SpringOut));
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
                LegendPanel.TranslateTo(0, -6, 160, Easing.SpringIn));
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
        SetFilterButtonState(FilterInterestsButton, _activeInterestFilter is not null);
        FilterInterestsButton.Text = _activeInterestFilter ?? "Interessi";
        SetFilterButtonState(FilterOpenNowButton, _viewModel.OpenNowOnly);
        SetFilterButtonState(FilterDistanceButton, _viewModel.MaxDistanceKm is not null);
        FilterDistanceButton.Text = FormatDistanceFilter(_viewModel.MaxDistanceKm);
    }

    private static void SetFilterButtonState(Button button, bool isActive)
    {
        button.BackgroundColor = isActive ? Color.FromArgb("#7C3AED") : Color.FromArgb("#F1F5F9");
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

    private void StartChatPolling()
    {
        if (_chatPollTimer is not null || Dispatcher is null) return;
        _chatPollTimer = Dispatcher.CreateTimer();
        _chatPollTimer.Interval = TimeSpan.FromSeconds(5);
        _chatPollTimer.Tick += async (_, _) =>
        {
            try
            {
                if (DirectMessageOverlay.IsVisible && _activeDirectMessageProfile is not null)
                {
                    await RefreshDirectMessageThreadAsync();
                }
                if (TableOverlay.IsVisible && _activeTableSummary is not null)
                {
                    await RefreshActiveTableThreadAsync();
                }
            }
            catch { /* ignore */ }
        };
        _chatPollTimer.Start();
    }

    private void StopChatPolling()
    {
        if (_chatPollTimer is null) return;
        _chatPollTimer.Stop();
        _chatPollTimer = null;
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
        await PresenceSheet.TranslateTo(0, 0, 260, Easing.SpringOut);
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
            await PresenceSheet.TranslateTo(0, 420, 200, Easing.SpringIn);
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
                BackgroundColor = Color.FromArgb("#F5F3FF"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Content = new Label
                {
                    Text = tag.Trim(),
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#6D28D9")
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
            BackgroundColor = Color.FromArgb("#F5F3FF"),
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
        await VenueDetailSheet.TranslateTo(0, 0, 260, Easing.SpringOut);

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
            await VenueDetailSheet.TranslateTo(0, 540, 200, Easing.SpringIn);
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
            await ProfileSheet.TranslateTo(0, 460, 200, Easing.SpringIn);
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
                    BackgroundColor = Color.FromArgb("#F5F3FF"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Content = new Label
                    {
                        Text = tag,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#6D28D9")
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
                    BackgroundColor = Color.FromArgb("#F5F3FF"),
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
        await SocialSheet.TranslateTo(0, 0, 260, Easing.SpringOut);
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
            await SocialSheet.TranslateTo(0, 480, 200, Easing.SpringIn);
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
            BackgroundColor = Color.FromArgb("#F5F3FF"),
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
            BackgroundColor = Color.FromArgb("#F5F3FF"),
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
            BackgroundColor = Color.FromArgb("#F5F3FF"),
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
            BackgroundColor = Color.FromArgb("#F5F3FF"),
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
            button.BackgroundColor = Color.FromArgb("#7C3AED");
            button.TextColor = Colors.White;
        }
        else
        {
            button.BackgroundColor = Color.FromArgb("#F1F5F9");
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

    private async Task ShowTableOverlayAsync(Guid tableId)
    {
        try
        {
            var tables = await _apiClient.GetMyTablesAsync();
            var table = tables.FirstOrDefault(t => t.TableId == tableId);
            if (table is null)
            {
                _viewModel.SetStatusMessage("Tavolo non trovato.");
                return;
            }
            await ShowTableOverlayAsync(table);
        }
        catch (Exception ex)
        {
            _viewModel.SetStatusMessage($"Impossibile aprire il tavolo: {ex.Message}");
        }
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
        await TableSheet.TranslateTo(0, 0, 260, Easing.SpringOut);
        await _chatHub.JoinThreadAsync($"table-{table.TableId}");
        await RefreshActiveTableThreadAsync();
    }

    private async Task HideTableOverlayAsync(bool animated)
    {
        if (!TableOverlay.IsVisible)
        {
            return;
        }

        if (_activeTableSummary is not null)
            await _chatHub.LeaveThreadAsync($"table-{_activeTableSummary.TableId}");

        if (animated)
        {
            await TableSheet.TranslateTo(0, 500, 200, Easing.SpringIn);
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
            BackgroundColor = Color.FromArgb("#F5F3FF"),
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
            BackgroundColor = message.IsMine ? Color.FromArgb("#7C3AED") : Color.FromArgb("#F5F3FF"),
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
            : Color.FromArgb("#6D28D9");
        SocialStatusLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private void SetTableStatus(string message, bool isError)
    {
        TableStatusLabel.Text = message;
        TableStatusLabel.TextColor = isError
            ? Color.FromArgb("#B91C1C")
            : Color.FromArgb("#6D28D9");
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
            : Color.FromArgb("#6D28D9");
        ProfileActionMessageLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private static Border CreateOverflowAvatar(int extraCount, double size)
    {
        return new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            BackgroundColor = Color.FromArgb("#F1F5F9"),
            Stroke = Colors.White,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
            Content = new Label
            {
                Text = $"+{extraCount}",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#6D28D9"),
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
            "#7C3AED",
            "#8B5CF6",
            "#EC4899",
            "#14B8A6",
            "#F97316",
            "#06B6D4"
        };

        var seed = string.IsNullOrWhiteSpace(displayName) ? nickname : displayName;
        if (string.IsNullOrWhiteSpace(seed))
        {
            return Color.FromArgb("#7C3AED");
        }

        var hash = seed.Aggregate(17, (current, ch) => current * 31 + ch);
        return Color.FromArgb(palette[Math.Abs(hash) % palette.Length]);
    }

}
