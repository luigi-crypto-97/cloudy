using System.Collections.ObjectModel;
using System.Windows.Input;
using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;

namespace FriendMap.Mobile.ViewModels;

public class StoriesViewModel : BindableObject
{
    private readonly ApiClient _apiClient;
    private bool _isBusy;

    public ObservableCollection<UserStory> Stories { get; } = new();

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

    public bool ShowEmptyState => !IsBusy && Stories.Count == 0;
    public ICommand RefreshCommand { get; }

    public StoriesViewModel(ApiClient apiClient)
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
            var items = await _apiClient.GetStoriesAsync();
            Stories.Clear();
            foreach (var item in items)
                Stories.Add(item);
        }
        catch { /* ignore */ }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task PostStoryAsync(string caption)
    {
        try
        {
            await _apiClient.PostStoryAsync(caption);
            AnalyticsService.TrackEvent("story_posted");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            AnalyticsService.TrackEvent("story_post_failed", new Dictionary<string, string> { ["error"] = ex.Message });
        }
    }
}
