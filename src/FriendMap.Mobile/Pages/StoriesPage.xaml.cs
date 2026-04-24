using FriendMap.Mobile.ViewModels;

namespace FriendMap.Mobile.Pages;

public partial class StoriesPage : ContentPage
{
    private readonly StoriesViewModel _vm;

    public StoriesPage(StoriesViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshAsync();
    }

    private async void OnAddStoryClicked(object? sender, EventArgs e)
    {
        var text = await DisplayPromptAsync("Nuova Story", "Scrivi qualcosa:");
        if (!string.IsNullOrWhiteSpace(text))
        {
            await _vm.PostStoryAsync(text);
        }
    }
}
