using System.Collections.ObjectModel;
using System.Windows.Input;
using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;

namespace FriendMap.Mobile.ViewModels;

public class MainMapViewModel : BindableObject
{
    private readonly ApiClient _apiClient;
    private bool _isBusy;
    private bool _isActionBusy;
    private VenueMarker? _selectedMarker;
    private string _searchQuery = string.Empty;
    private string _selectedCategory = "all";
    private bool _openNowOnly;
    private double? _maxDistanceKm;
    private string? _statusMessage;
    private string? _actionMessage;
    private Color _statusColor = Color.FromArgb("#B91C1C");

    public ObservableCollection<VenueMarker> Markers { get; } = new();
    public MapViewport CurrentViewport { get; private set; } = MapViewport.MilanDefault;
    public IReadOnlyList<MapArea> Areas { get; private set; } = Array.Empty<MapArea>();

    public event EventHandler? MarkersRefreshed;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public bool IsActionBusy
    {
        get => _isActionBusy;
        set
        {
            _isActionBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AreVenueActionsEnabled));
        }
    }

    public VenueMarker? SelectedMarker
    {
        get => _selectedMarker;
        set
        {
            _selectedMarker = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedMarker));
            OnPropertyChanged(nameof(AreVenueActionsEnabled));
            OnPropertyChanged(nameof(SelectedVenueMeta));
            OnPropertyChanged(nameof(SelectedPeopleLabel));
            OnPropertyChanged(nameof(SelectedDensityLabel));
            OnPropertyChanged(nameof(SelectedCheckInLabel));
            OnPropertyChanged(nameof(SelectedIntentionLabel));
            OnPropertyChanged(nameof(SelectedTableLabel));
            OnPropertyChanged(nameof(SelectedAddressLabel));
            OnPropertyChanged(nameof(SelectedOpenNowLabel));
            OnPropertyChanged(nameof(HasSelectedPresencePreview));
            OnPropertyChanged(nameof(SelectedPhoneLabel));
            OnPropertyChanged(nameof(SelectedWebsiteLabel));
            OnPropertyChanged(nameof(SelectedHoursLabel));
            OnPropertyChanged(nameof(HasSelectedPhone));
            OnPropertyChanged(nameof(HasSelectedWebsite));
            OnPropertyChanged(nameof(HasSelectedHours));
        }
    }

    public bool HasSelectedMarker => SelectedMarker is not null;

    public string? StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatusMessage));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(MapSummaryLabel));
        }
    }

    public Color StatusColor
    {
        get => _statusColor;
        set
        {
            _statusColor = value;
            OnPropertyChanged();
        }
    }

    public string? ActionMessage
    {
        get => _actionMessage;
        set
        {
            _actionMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasActionMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasActionMessage => !string.IsNullOrWhiteSpace(ActionMessage);

    public bool HasMarkers => Markers.Count > 0;

    public bool ShowEmptyState => !IsBusy && !HasMarkers && !HasStatusMessage;

    public bool AreVenueActionsEnabled => SelectedMarker is not null && !IsActionBusy;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value;
            OnPropertyChanged();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
        }
    }

    public bool OpenNowOnly
    {
        get => _openNowOnly;
        set
        {
            _openNowOnly = value;
            OnPropertyChanged();
        }
    }

    public double? MaxDistanceKm
    {
        get => _maxDistanceKm;
        set
        {
            _maxDistanceKm = value;
            OnPropertyChanged();
        }
    }

    public string MapSummaryLabel => Markers.Count switch
    {
        0 => "Nessun luogo attivo",
        1 => "1 luogo attivo ora",
        _ => $"{Markers.Count} luoghi attivi ora"
    };

    public string SelectedVenueMeta => SelectedMarker is null
        ? string.Empty
        : $"{SelectedMarker.PeopleEstimate} persone • {FormatDensityLabel(SelectedMarker.DensityLevel)} • {SelectedMarker.City}";

    public string SelectedPeopleLabel => SelectedMarker is null
        ? string.Empty
        : $"{SelectedMarker.PeopleEstimate} persone";

    public string SelectedDensityLabel => SelectedMarker is null
        ? string.Empty
        : FormatDensityLabel(SelectedMarker.DensityLevel);

    public string SelectedCheckInLabel => SelectedMarker is null
        ? string.Empty
        : $"{SelectedMarker.ActiveCheckIns} check-in";

    public string SelectedIntentionLabel => SelectedMarker is null
        ? string.Empty
        : $"{SelectedMarker.ActiveIntentions} piani";

    public string SelectedTableLabel => SelectedMarker is null
        ? string.Empty
        : $"{SelectedMarker.OpenTables} tavoli";

    public string SelectedAddressLabel => SelectedMarker is null
        ? string.Empty
        : $"{SelectedMarker.AddressLine}, {SelectedMarker.City}";

    public string SelectedPhoneLabel => SelectedMarker?.PhoneNumber ?? string.Empty;

    public string SelectedWebsiteLabel => SelectedMarker is null
        ? string.Empty
        : FormatWebsiteLabel(SelectedMarker.WebsiteUrl);

    public string SelectedHoursLabel => SelectedMarker?.HoursSummary ?? string.Empty;

    public string SelectedOpenNowLabel => SelectedMarker is null
        ? string.Empty
        : SelectedMarker.IsOpenNow ? "Open now" : "Orari stimati";

    public bool HasSelectedPresencePreview => SelectedMarker?.PresencePreview?.Count > 0;
    public bool HasSelectedPhone => !string.IsNullOrWhiteSpace(SelectedMarker?.PhoneNumber);
    public bool HasSelectedWebsite => !string.IsNullOrWhiteSpace(SelectedMarker?.WebsiteUrl);
    public bool HasSelectedHours => !string.IsNullOrWhiteSpace(SelectedMarker?.HoursSummary);

    public ICommand RefreshCommand { get; }
    public ICommand CheckInCommand { get; }
    public ICommand PlanIntentionCommand { get; }
    public ICommand CreateTableCommand { get; }
    public ICommand DismissSelectionCommand { get; }

    public MainMapViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        Markers.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasMarkers));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(MapSummaryLabel));
        };
        RefreshCommand = new Command(async () => await RefreshAsync());
        CheckInCommand = new Command(async () => await CheckInAsync());
        PlanIntentionCommand = new Command(async () => await PlanIntentionAsync());
        CreateTableCommand = new Command(async () => await CreateTableAsync());
        DismissSelectionCommand = new Command(ClearSelection);
    }

    public async Task RefreshAsync()
    {
        await RefreshAsync(CurrentViewport);
    }

    public async Task RefreshAsync(MapViewport viewport)
    {
        if (IsBusy) return;
        IsBusy = true;
        OnPropertyChanged(nameof(ShowEmptyState));
        CurrentViewport = viewport.Normalize();

        try
        {
            var layer = await _apiClient.GetMapLayerAsync(
                CurrentViewport,
                string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery,
                SelectedCategory == "all" ? null : SelectedCategory,
                OpenNowOnly,
                MaxDistanceKm);
            var markers = layer.Markers ?? new List<VenueMarker>();
            Markers.Clear();
            foreach (var marker in markers)
            {
                Markers.Add(marker);
            }
            Areas = layer.Areas ?? new List<MapArea>();

            if (markers.Count > 0 || SelectedMarker is not null)
            {
                StatusMessage = null;
            }

            if (SelectedMarker is not null)
            {
                SelectedMarker = Markers.FirstOrDefault(x => x.VenueId == SelectedMarker.VenueId);
            }

            if (SelectedMarker is null)
            {
                SelectedMarker = Markers.FirstOrDefault();
            }

            OnPropertyChanged(nameof(HasMarkers));
            OnPropertyChanged(nameof(ShowEmptyState));
            MarkersRefreshed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Markers.Clear();
            Areas = Array.Empty<MapArea>();
            SelectedMarker = null;
            ActionMessage = null;
            StatusColor = Color.FromArgb("#B91C1C");
            StatusMessage = $"Impossibile caricare la mappa. Verifica Backend URL e API LAN. Dettaglio: {_apiClient.DescribeException(ex)}";
            OnPropertyChanged(nameof(HasMarkers));
            OnPropertyChanged(nameof(ShowEmptyState));
            MarkersRefreshed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public void SelectMarker(VenueMarker marker)
    {
        if (SelectedMarker?.VenueId == marker.VenueId)
        {
            ClearSelection();
            return;
        }

        SelectedMarker = marker;
        ActionMessage = null;
    }

    public void ClearSelection()
    {
        SelectedMarker = null;
        ActionMessage = null;
    }

    public void SetStatusMessage(string message)
    {
        StatusColor = Color.FromArgb("#B91C1C");
        StatusMessage = message;
    }

    public void ApplyDiscoveryFilters(string searchQuery, string category, bool openNowOnly, double? maxDistanceKm)
    {
        SearchQuery = searchQuery;
        SelectedCategory = string.IsNullOrWhiteSpace(category) ? "all" : category;
        OpenNowOnly = openNowOnly;
        MaxDistanceKm = maxDistanceKm;
    }

    public async Task RequestPermissionsAndRegisterDeviceAsync(IDevicePermissionService permissions)
    {
        await permissions.RequestMapAndPushPermissionsAsync();
        await _apiClient.RegisterStoredDeviceTokenAsync();
    }

    public async Task RegisterStoredDeviceTokenAsync()
    {
        await _apiClient.RegisterStoredDeviceTokenAsync();
    }

    private async Task CheckInAsync()
    {
        if (SelectedMarker is null) return;
        await RunVenueActionAsync("Check-in registrato.", async userId =>
        {
            await _apiClient.CheckInAsync(userId, SelectedMarker.VenueId, 180);
        });
    }

    private async Task PlanIntentionAsync()
    {
        if (SelectedMarker is null) return;
        await RunVenueActionAsync("Intenzione creata per stasera.", async userId =>
        {
            var starts = DateTimeOffset.UtcNow.AddHours(1);
            await _apiClient.CreateIntentionAsync(
                userId,
                SelectedMarker.VenueId,
                starts,
                starts.AddHours(3),
                "Ci vado piu tardi");
        });
    }

    private async Task CreateTableAsync()
    {
        if (SelectedMarker is null) return;
        await RunVenueActionAsync("Tavolo sociale creato.", async userId =>
        {
            await _apiClient.CreateSocialTableAsync(
                userId,
                SelectedMarker.VenueId,
                $"Tavolo a {SelectedMarker.Name}",
                "Creato dall'app iOS dev",
                DateTimeOffset.UtcNow.AddHours(1),
                6,
                "auto");
        });
    }

    private async Task RunVenueActionAsync(string successMessage, Func<Guid, Task> action)
    {
        if (IsActionBusy) return;
        IsActionBusy = true;
        ActionMessage = null;

        try
        {
            var userId = await _apiClient.GetCurrentUserIdAsync();
            await action(userId);
            ActionMessage = successMessage;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ActionMessage = _apiClient.DescribeException(ex);
        }
        finally
        {
            IsActionBusy = false;
        }
    }

    public Color ResolveBubbleColor(int intensity)
    {
        return intensity switch
        {
            <= 0 => Colors.Transparent,
            < 25 => Color.FromArgb("#DBEAFE"),
            < 45 => Color.FromArgb("#93C5FD"),
            < 65 => Color.FromArgb("#60A5FA"),
            < 85 => Color.FromArgb("#2563EB"),
            _ => Color.FromArgb("#1D4ED8")
        };
    }

    private static string FormatDensityLabel(string densityLevel)
    {
        return densityLevel?.Trim().ToLowerInvariant() switch
        {
            "low" => "Affluenza bassa",
            "medium" => "Affluenza media",
            "high" => "Affluenza alta",
            "full" => "Quasi pieno",
            _ => "Affluenza stimata"
        };
    }

    private static string FormatWebsiteLabel(string? websiteUrl)
    {
        if (string.IsNullOrWhiteSpace(websiteUrl))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(websiteUrl, UriKind.Absolute, out var uri))
        {
            return websiteUrl;
        }

        return uri.Host.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
