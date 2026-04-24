using System.Collections.ObjectModel;
using System.Windows.Input;
using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;

namespace FriendMap.Mobile.ViewModels;

public class GamificationViewModel : BindableObject
{
    private readonly ApiClient _apiClient;
    private bool _isBusy;

    public ObservableCollection<UserAchievement> Achievements { get; } = new();

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

    public bool ShowEmptyState => !IsBusy && Achievements.Count == 0;
    public ICommand RefreshCommand { get; }

    public GamificationViewModel(ApiClient apiClient)
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
            await _apiClient.CheckAchievementsAsync();
            var items = await _apiClient.GetMyAchievementsAsync();
            Achievements.Clear();
            foreach (var item in items)
                Achievements.Add(item);
        }
        catch { /* ignore */ }
        finally
        {
            IsBusy = false;
        }
    }
}
