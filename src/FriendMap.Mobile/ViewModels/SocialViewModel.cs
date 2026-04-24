using System.Collections.ObjectModel;
using System.Windows.Input;
using FriendMap.Mobile.Models;
using FriendMap.Mobile.Pages;
using FriendMap.Mobile.Services;

namespace FriendMap.Mobile.ViewModels;

public class SocialViewModel : BindableObject
{
    private readonly ApiClient _apiClient;
    private readonly AppIntentService _appIntentService;
    private bool _isBusy;
    private bool _isActionBusy;
    private string? _statusMessage;

    public ObservableCollection<SocialTableSummary> Tables { get; } = new();
    public ObservableCollection<DirectMessageThreadSummary> Threads { get; } = new();
    public ObservableCollection<SocialConnection> Friends { get; } = new();
    public ObservableCollection<SocialConnection> PendingRequests { get; } = new();
    public ObservableCollection<UserSearchResult> SearchResults { get; } = new();
    public ObservableCollection<ContactMatchResult> ContactMatches { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEmptyStateVisible));
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
    public bool IsEmptyStateVisible => !IsBusy && Tables.Count == 0 && Threads.Count == 0 && Friends.Count == 0 && PendingRequests.Count == 0 && SearchResults.Count == 0 && ContactMatches.Count == 0;

    public ICommand RefreshCommand { get; }
    public ICommand AcceptFriendCommand { get; }
    public ICommand RejectFriendCommand { get; }
    public ICommand OpenChatCommand { get; }
    public ICommand OpenTableCommand { get; }
    public ICommand AddFriendCommand { get; }
    public ICommand OpenProfileCommand { get; }

    public SocialViewModel(ApiClient apiClient, AppIntentService appIntentService)
    {
        _apiClient = apiClient;
        _appIntentService = appIntentService;
        RefreshCommand = new Command(async () => await RefreshAsync());
        AcceptFriendCommand = new Command<SocialConnection?>(async c => await AcceptFriendAsync(c));
        RejectFriendCommand = new Command<SocialConnection?>(async c => await RejectFriendAsync(c));
        OpenChatCommand = new Command<DirectMessageThreadSummary?>(async t => await OpenChatAsync(t));
        OpenTableCommand = new Command<SocialTableSummary?>(async t => await OpenTableAsync(t));
        AddFriendCommand = new Command<UserSearchResult?>(async u => await AddFriendAsync(u));
        OpenProfileCommand = new Command<Guid>(async userId => await OpenProfileAsync(userId));

        // Notifica cambiamenti di stato vuoto quando le collezioni cambiano
        Tables.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyStateVisible));
        Threads.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyStateVisible));
        Friends.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyStateVisible));
        PendingRequests.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyStateVisible));
        SearchResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyStateVisible));
        ContactMatches.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyStateVisible));
    }

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;
        SearchResults.Clear();

        try
        {
            var hub = await _apiClient.GetSocialHubAsync();
            var tables = await _apiClient.GetMyTablesAsync();
            var inbox = await _apiClient.GetDirectMessageInboxAsync();

            Tables.Clear();
            foreach (var t in tables)
                Tables.Add(t);

            Threads.Clear();
            foreach (var th in inbox)
                Threads.Add(th);

            Friends.Clear();
            PendingRequests.Clear();
            foreach (var c in hub.Friends)
                Friends.Add(c);
            foreach (var c in hub.IncomingRequests)
                PendingRequests.Add(c);

            LocalCacheService.Set("social_hub", hub, TimeSpan.FromMinutes(5));
            LocalCacheService.Set("social_tables", tables, TimeSpan.FromMinutes(5));
            LocalCacheService.Set("social_inbox", inbox, TimeSpan.FromMinutes(5));
        }
        catch (Exception ex)
        {
            LoadFromCache();
            if (Tables.Count == 0 && Threads.Count == 0)
                StatusMessage = _apiClient.DescribeException(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadFromCache()
    {
        var hub = LocalCacheService.Get<SocialHub>("social_hub");
        var tables = LocalCacheService.Get<List<SocialTableSummary>>("social_tables");
        var inbox = LocalCacheService.Get<List<DirectMessageThreadSummary>>("social_inbox");

        if (tables is not null)
        {
            Tables.Clear();
            foreach (var t in tables) Tables.Add(t);
        }
        if (inbox is not null)
        {
            Threads.Clear();
            foreach (var th in inbox) Threads.Add(th);
        }
        if (hub is not null)
        {
            Friends.Clear();
            PendingRequests.Clear();
            foreach (var c in hub.Friends) Friends.Add(c);
            foreach (var c in hub.IncomingRequests) PendingRequests.Add(c);
        }
    }

    private async Task AcceptFriendAsync(SocialConnection? connection)
    {
        if (connection is null || IsActionBusy) return;
        try
        {
            IsActionBusy = true;
            HapticService.Medium();
            await _apiClient.AcceptFriendRequestAsync(connection.UserId);
            HapticService.Success();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = _apiClient.DescribeException(ex);
        }
        finally
        {
            IsActionBusy = false;
        }
    }

    private async Task RejectFriendAsync(SocialConnection? connection)
    {
        if (connection is null || IsActionBusy) return;
        try
        {
            IsActionBusy = true;
            HapticService.Light();
            await _apiClient.RejectFriendRequestAsync(connection.UserId);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = _apiClient.DescribeException(ex);
        }
        finally
        {
            IsActionBusy = false;
        }
    }

    private async Task AddFriendAsync(UserSearchResult? user)
    {
        if (user is null || IsActionBusy) return;
        try
        {
            IsActionBusy = true;
            HapticService.Medium();
            await _apiClient.SendFriendRequestAsync(user.UserId);
            HapticService.Success();
            // Rimuovi dai risultati
            SearchResults.Remove(user);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = _apiClient.DescribeException(ex);
        }
        finally
        {
            IsActionBusy = false;
        }
    }

    public async Task OpenChatAsync(DirectMessageThreadSummary? thread)
    {
        if (thread is null) return;
        await OpenDirectMessageAsync(thread.OtherUserId);
    }

    public async Task OpenTableAsync(SocialTableSummary? table)
    {
        if (table is null) return;
        HapticService.Light();
        await Shell.Current.GoToAsync($"{nameof(SocialTablePage)}?tableId={Uri.EscapeDataString(table.TableId.ToString())}");
    }

    public async Task OpenProfileAsync(Guid userId)
    {
        if (userId == Guid.Empty) return;
        HapticService.Light();
        await Shell.Current.GoToAsync($"{nameof(SocialProfilePage)}?userId={Uri.EscapeDataString(userId.ToString())}");
    }

    public async Task OpenDirectMessageAsync(Guid userId)
    {
        if (userId == Guid.Empty) return;
        HapticService.Light();
        await Shell.Current.GoToAsync($"{nameof(SocialChatPage)}?userId={Uri.EscapeDataString(userId.ToString())}");
    }

    public async Task SearchUsersAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2) return;
        try
        {
            var results = await _apiClient.SearchUsersAsync(query);
            SearchResults.Clear();
            foreach (var r in results)
                SearchResults.Add(r);
            AnalyticsService.TrackEvent("user_search", new Dictionary<string, string> { ["query"] = query });
            OnPropertyChanged(nameof(IsEmptyStateVisible));
        }
        catch (Exception ex)
        {
            StatusMessage = _apiClient.DescribeException(ex);
        }
    }

    public void SetContactMatches(IEnumerable<ContactMatchResult> matches)
    {
        ContactMatches.Clear();
        foreach (var match in matches)
        {
            ContactMatches.Add(match);
        }

        OnPropertyChanged(nameof(IsEmptyStateVisible));
    }
}
