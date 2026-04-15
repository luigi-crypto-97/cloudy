using System.Collections.ObjectModel;
using System.Windows.Input;
using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;

namespace FriendMap.Mobile.ViewModels;

public class MainMapViewModel : BindableObject
{
    private readonly ApiClient _apiClient;
    private bool _isBusy;

    public ObservableCollection<VenueMarker> Markers { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand { get; }

    public MainMapViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new Command(async () => await RefreshAsync());
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
        }
        finally
        {
            IsBusy = false;
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
