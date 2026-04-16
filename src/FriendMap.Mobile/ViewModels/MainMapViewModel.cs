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
    private string? _lastActionMessage;

    public ObservableCollection<VenueMarker> Markers { get; } = new();

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
        }
    }

    public bool HasSelectedMarker => SelectedMarker is not null;

    public string? LastActionMessage
    {
        get => _lastActionMessage;
        set
        {
            _lastActionMessage = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand CheckInCommand { get; }
    public ICommand PlanIntentionCommand { get; }
    public ICommand CreateTableCommand { get; }

    public MainMapViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new Command(async () => await RefreshAsync());
        CheckInCommand = new Command(async () => await CheckInAsync());
        PlanIntentionCommand = new Command(async () => await PlanIntentionAsync());
        CreateTableCommand = new Command(async () => await CreateTableAsync());
    }

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var markers = await _apiClient.GetVenueMarkersAsync();
            Markers.Clear();
            foreach (var marker in markers)
            {
                Markers.Add(marker);
            }

            if (SelectedMarker is not null)
            {
                SelectedMarker = Markers.FirstOrDefault(x => x.VenueId == SelectedMarker.VenueId);
            }

            if (SelectedMarker is null)
            {
                SelectedMarker = Markers.FirstOrDefault();
            }

            MarkersRefreshed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SelectMarker(VenueMarker marker)
    {
        SelectedMarker = marker;
        LastActionMessage = null;
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
        LastActionMessage = null;

        try
        {
            var userId = await _apiClient.GetCurrentUserIdAsync();
            await action(userId);
            LastActionMessage = successMessage;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            LastActionMessage = ex.Message;
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
}
