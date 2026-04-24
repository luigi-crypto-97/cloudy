namespace FriendMap.Mobile.Pages;

public partial class OnboardingPage : ContentPage
{
    public OnboardingPage()
    {
        InitializeComponent();
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        Services.HapticService.Medium();
        Preferences.Set("has_seen_onboarding", true);
        await Shell.Current.GoToAsync("//login");
    }
}
