using FriendMap.Mobile.Services;
using FriendMap.Mobile.ViewModels;

namespace FriendMap.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel)
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
            if (await _viewModel.TryRestoreAsync())
            {
                await Shell.Current.GoToAsync(_viewModel.ResolveAuthenticatedRoute());
                return;
            }
        }
        catch (Exception ex)
        {
            _viewModel.Error = ex.Message;
        }

        // Bump-style entrance animation
        await AnimateEntranceAsync();
    }

    private async Task AnimateEntranceAsync()
    {
        HeroLayout.Opacity = 0;
        HeroLayout.TranslationY = 24;
        FormLayout.Opacity = 0;
        FormLayout.TranslationY = 32;
        FooterLayout.Opacity = 0;
        HintLabel.Opacity = 0;

        await Task.WhenAll(
            HeroLayout.FadeTo(1, 600, Easing.CubicOut),
            HeroLayout.TranslateTo(0, 0, 600, Easing.CubicOut)
        );

        await Task.WhenAll(
            FormLayout.FadeTo(1, 600, Easing.CubicOut),
            FormLayout.TranslateTo(0, 0, 600, Easing.CubicOut)
        );

        await Task.WhenAll(
            FooterLayout.FadeTo(1, 400, Easing.CubicOut),
            HintLabel.FadeTo(1, 400, Easing.CubicOut)
        );
    }

    private void OnLoginClicked(object? sender, EventArgs e)
    {
        HapticService.Medium();
    }
}
