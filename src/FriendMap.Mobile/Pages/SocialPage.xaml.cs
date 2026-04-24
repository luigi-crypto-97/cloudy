using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;
using FriendMap.Mobile.ViewModels;

namespace FriendMap.Mobile.Pages;

public partial class SocialPage : ContentPage
{
    private readonly SocialViewModel _viewModel;

    public SocialPage(SocialViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
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
