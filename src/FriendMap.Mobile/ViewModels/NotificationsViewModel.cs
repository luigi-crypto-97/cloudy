using System.Collections.ObjectModel;
using System.Windows.Input;
using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;

namespace FriendMap.Mobile.ViewModels;

public class NotificationsViewModel : BindableObject
{
    private readonly ApiClient _apiClient;
    private bool _isBusy;
    private string? _statusMessage;

    public ObservableCollection<NotificationItem> Items { get; } = new();

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

    public string? StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool ShowEmptyState => !IsBusy && Items.Count == 0;

    public ICommand RefreshCommand { get; }

    public NotificationsViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new Command(async () => await RefreshAsync());
    }

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var items = await _apiClient.GetNotificationsOutboxAsync();
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
            LocalCacheService.Set("notifications", items, TimeSpan.FromMinutes(10));
        }
        catch (Exception ex)
        {
            var cached = LocalCacheService.Get<List<NotificationItem>>("notifications");
            if (cached is not null)
            {
                Items.Clear();
                foreach (var item in cached) Items.Add(item);
            }
            else
            {
                StatusMessage = _apiClient.DescribeException(ex);
            }
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }
}
