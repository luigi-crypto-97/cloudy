using System.Collections.ObjectModel;
using System.Windows.Input;
using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;

namespace FriendMap.Mobile.ViewModels;

public class DiscoveryViewModel : BindableObject
{
    private readonly ApiClient _apiClient;
    private bool _isBusy;

    public ObservableCollection<NearbyUser> NearbyUsers { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public bool ShowEmptyState => !IsBusy && NearbyUsers.Count == 0;
    public ICommand RefreshCommand { get; }

    public DiscoveryViewModel(ApiClient apiClient)
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
            var items = await _apiClient.GetNearbyUsersAsync();
            NearbyUsers.Clear();
            foreach (var item in items)
                NearbyUsers.Add(item);
        }
        catch { /* ignore */ }
        finally
        {
            IsBusy = false;
        }
    }
}
