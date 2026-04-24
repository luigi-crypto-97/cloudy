using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;
using FriendMap.Mobile.ViewModels;
using Microsoft.Maui.ApplicationModel.Communication;
using PermissionStatus = Microsoft.Maui.ApplicationModel.PermissionStatus;

namespace FriendMap.Mobile.Pages;

public partial class SocialPage : ContentPage
{
    private readonly SocialViewModel _viewModel;
    private readonly IDevicePermissionService _permissions;
    private readonly ApiClient _apiClient;

    public SocialPage(SocialViewModel viewModel, IDevicePermissionService permissions, ApiClient apiClient)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _permissions = permissions;
        _apiClient = apiClient;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.RefreshAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SocialPage] Refresh error: {ex}");
        }
    }

    private void OnSearchCompleted(object? sender, EventArgs e) => PerformSearch();
    private void OnSearchClicked(object? sender, EventArgs e) => PerformSearch();

    private async void PerformSearch()
    {
        var query = SearchEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            await DisplayAlert("Ricerca", "Inserisci almeno 2 caratteri.", "OK");
            return;
        }

        try
        {
            await _viewModel.SearchUsersAsync(query);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SocialPage] Search error: {ex}");
            await DisplayAlert("Errore", "Impossibile cercare. Riprova pi&#xF9; tardi.", "OK");
        }
    }

    private async void OnInviteFriendsClicked(object? sender, EventArgs e)
    {
        HapticService.Light();
        try
        {
            await Shell.Current.GoToAsync("//main/social/invite");
        }
        catch
        {
            // Fallback se la route non &#xE8; registrata
            await DisplayAlert("Invita amici", "Condividi il link o cerca amici nella ricerca.", "OK");
        }
    }

    private void OnSearchFriendsClicked(object? sender, EventArgs e)
    {
        SearchEntry.Focus();
    }

    private async void OnSyncContactsClicked(object? sender, EventArgs e)
    {
        try
        {
            var status = await _permissions.GetContactsStatusAsync();
            if (status != PermissionStatus.Granted)
            {
                status = await _permissions.RequestContactsAsync();
            }

            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Rubrica", "Consenti l'accesso ai contatti per trovare chi usa già Cloudy.", "OK");
                return;
            }

            var contacts = await Microsoft.Maui.ApplicationModel.Communication.Contacts.Default.GetAllAsync();
            var phones = contacts
                .SelectMany(x => x.Phones ?? Enumerable.Empty<ContactPhone>())
                .Select(x => x.PhoneNumber)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var emails = contacts
                .SelectMany(x => x.Emails ?? Enumerable.Empty<ContactEmail>())
                .Select(x => x.EmailAddress)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var matches = await _apiClient.MatchContactsAsync(phones, emails);
            _viewModel.SetContactMatches(matches);
            await DisplayAlert("Rubrica", matches.Count == 0 ? "Nessun contatto trovato su Cloudy." : $"Trovati {matches.Count} contatti già presenti.", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SocialPage] SyncContacts error: {ex}");
            await DisplayAlert("Rubrica", _apiClient.DescribeException(ex), "OK");
        }
    }

    private async void OnOpenRecapClicked(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(SocialRecapPage));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Recap", _apiClient.DescribeException(ex), "OK");
        }
    }

    private async void OnMessageTapped(object? sender, TappedEventArgs e)
    {
        if (sender is View view && view.BindingContext is DirectMessageThreadSummary thread)
        {
            try
            {
                await _viewModel.OpenChatAsync(thread);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialPage] OpenChat error: {ex}");
                await DisplayAlert("Errore", "Impossibile aprire la chat.", "OK");
            }
        }
    }

    private async void OnSearchResultTapped(object? sender, TappedEventArgs e)
    {
        if (sender is View view && view.BindingContext is UserSearchResult result)
        {
            try
            {
                await _viewModel.OpenProfileAsync(result.UserId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialPage] OpenProfile(search) error: {ex}");
                await DisplayAlert("Errore", "Impossibile aprire il profilo.", "OK");
            }
        }
    }

    private async void OnContactMatchTapped(object? sender, TappedEventArgs e)
    {
        if (sender is View view && view.BindingContext is ContactMatchResult result)
        {
            try
            {
                await _viewModel.OpenProfileAsync(result.UserId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialPage] OpenProfile(contact) error: {ex}");
                await DisplayAlert("Errore", "Impossibile aprire il profilo.", "OK");
            }
        }
    }

    private async void OnConnectionTapped(object? sender, TappedEventArgs e)
    {
        if (sender is View view && view.BindingContext is SocialConnection connection)
        {
            try
            {
                await _viewModel.OpenProfileAsync(connection.UserId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialPage] OpenProfile(connection) error: {ex}");
                await DisplayAlert("Errore", "Impossibile aprire il profilo.", "OK");
            }
        }
    }

    private async void OnFriendChatClicked(object? sender, EventArgs e)
    {
        if (sender is View view && view.BindingContext is SocialConnection connection)
        {
            try
            {
                await _viewModel.OpenDirectMessageAsync(connection.UserId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialPage] FriendChat error: {ex}");
                await DisplayAlert("Errore", "Impossibile aprire la chat.", "OK");
            }
        }
    }

    private async void OnTableTapped(object? sender, TappedEventArgs e)
    {
        if (sender is View view && view.BindingContext is SocialTableSummary table)
        {
            try
            {
                await _viewModel.OpenTableAsync(table);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SocialPage] OpenTable error: {ex}");
                await DisplayAlert("Errore", "Impossibile aprire il tavolo.", "OK");
            }
        }
    }
}
