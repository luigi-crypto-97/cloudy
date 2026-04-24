namespace FriendMap.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    public ProfilePage()
    {
        InitializeComponent();
    }

    private void OnGhostModeToggled(object? sender, ToggledEventArgs e)
    {
        Services.HapticService.Light();
    }

    private void OnDarkModeToggled(object? sender, ToggledEventArgs e)
    {
        Services.HapticService.Light();
        Application.Current!.UserAppTheme = e.Value ? AppTheme.Dark : AppTheme.Light;
    }

    private async void OnEditProfileClicked(object? sender, EventArgs e)
    {
        Services.HapticService.Light();
        try
        {
            // Apri l'overlay modifica profilo sulla tab Mappa (pi&#xF9; robusto della pagina separata)
            await Shell.Current.GoToAsync("//main/map");
            await Task.Delay(200);
            MessagingCenter.Send(this, "OpenEditProfile");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Errore", $"Impossibile aprire il profilo: {ex.Message}", "OK");
        }
    }

    private async void OnInterestsClicked(object? sender, EventArgs e)
    {
        Services.HapticService.Light();
        await Shell.Current.GoToAsync("//interests");
    }

    private async void OnBadgesClicked(object? sender, EventArgs e)
    {
        Services.HapticService.Light();
        await Shell.Current.GoToAsync("gamification");
    }

    private async void OnInviteClicked(object? sender, EventArgs e)
    {
        Services.HapticService.Light();
        await Shell.Current.GoToAsync("invite");
    }

    private async void OnPrivacyClicked(object? sender, EventArgs e)
    {
        Services.HapticService.Light();
        await Shell.Current.GoToAsync("privacy");
    }

    private async void OnTermsClicked(object? sender, EventArgs e)
    {
        Services.HapticService.Light();
        await Shell.Current.GoToAsync("terms");
    }

    private async void OnDeleteAccountClicked(object? sender, EventArgs e)
    {
        Services.HapticService.Heavy();
        bool confirm = await DisplayAlert("Elimina account", "Questa azione è irreversibile. Tutti i tuoi dati verranno cancellati.", "Elimina", "Annulla");
        if (!confirm) return;

        try
        {
            var api = Application.Current?.Handler?.MauiContext?.Services.GetService<FriendMap.Mobile.Services.ApiClient>();
            if (api is not null)
            {
                await api.DeleteAccountAsync();
            }
            SecureStorage.Remove("friendmap_token");
            await Shell.Current.GoToAsync("//login");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Errore", $"Impossibile eliminare l'account: {ex.Message}", "OK");
        }
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        Services.HapticService.Medium();
        bool confirm = await DisplayAlert("Uscita", "Vuoi davvero uscire?", "Esci", "Annulla");
        if (confirm)
        {
            SecureStorage.Remove("friendmap_token");
            await Shell.Current.GoToAsync("//login");
        }
    }
}
