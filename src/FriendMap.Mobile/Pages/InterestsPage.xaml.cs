using FriendMap.Mobile.Services;
using FriendMap.Mobile.ViewModels;

namespace FriendMap.Mobile.Pages;

public partial class InterestsPage : ContentPage
{
    private readonly InterestsViewModel _viewModel;
    private double _panStartX;
    private double _panCurrentX;
    private const double SwipeThreshold = 120;
    private bool _isAnimating;

    public InterestsPage(InterestsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InterestsViewModel.CurrentIndex))
            {
                UpdateCardContent();
            }
            if (e.PropertyName == nameof(InterestsViewModel.IsComplete))
            {
                if (_viewModel.IsComplete)
                {
                    _ = AnimateCompletionAsync();
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateCardContent();
    }

    private void UpdateCardContent()
    {
        if (_viewModel.CurrentIndex >= _viewModel.Cards.Count) return;
        var card = _viewModel.Cards[_viewModel.CurrentIndex];
        CardEmoji.Text = card.Emoji;
        CardTitle.Text = card.Tag;
        CardDescription.Text = card.Description;
        ActiveCard.TranslationX = 0;
        ActiveCard.Rotation = 0;
        ActiveCard.Opacity = 1;
        LikeBadge.Opacity = 0;
        LikeBadge.IsVisible = false;
        DislikeBadge.Opacity = 0;
        DislikeBadge.IsVisible = false;
    }

    private async Task AnimateCompletionAsync()
    {
        CompleteLayout.Opacity = 0;
        CompleteLayout.TranslationY = 20;
        await Task.WhenAll(
            CompleteLayout.FadeTo(1, 600, Easing.CubicOut),
            CompleteLayout.TranslateTo(0, 0, 600, Easing.SpringOut)
        );
    }

    private void OnCardPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_isAnimating || _viewModel.IsComplete) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartX = ActiveCard.TranslationX;
                break;
            case GestureStatus.Running:
                _panCurrentX = _panStartX + e.TotalX;
                ActiveCard.TranslationX = _panCurrentX;
                ActiveCard.Rotation = _panCurrentX * 0.05;
                var progress = Math.Abs(_panCurrentX) / SwipeThreshold;
                progress = Math.Min(progress, 1);
                if (_panCurrentX > 0)
                {
                    LikeBadge.IsVisible = true;
                    LikeBadge.Opacity = progress;
                    DislikeBadge.IsVisible = false;
                }
                else
                {
                    DislikeBadge.IsVisible = true;
                    DislikeBadge.Opacity = progress;
                    LikeBadge.IsVisible = false;
                }
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (_panCurrentX > SwipeThreshold)
                {
                    _ = SwipeCardAsync(liked: true);
                }
                else if (_panCurrentX < -SwipeThreshold)
                {
                    _ = SwipeCardAsync(liked: false);
                }
                else
                {
                    _ = ResetCardAsync();
                }
                break;
        }
    }

    private async Task SwipeCardAsync(bool liked)
    {
        if (_isAnimating) return;
        _isAnimating = true;
        HapticService.Medium();

        var direction = liked ? 1 : -1;
        await Task.WhenAll(
            ActiveCard.TranslateTo(direction * 400, ActiveCard.TranslationY, 250, Easing.CubicOut),
            ActiveCard.FadeTo(0, 250, Easing.CubicOut),
            ActiveCard.RotateTo(direction * 30, 250, Easing.CubicOut)
        );

        _viewModel.Swipe(liked);
        _isAnimating = false;
    }

    private async Task ResetCardAsync()
    {
        await Task.WhenAll(
            ActiveCard.TranslateTo(0, 0, 200, Easing.SpringOut),
            ActiveCard.RotateTo(0, 200, Easing.SpringOut)
        );
        LikeBadge.Opacity = 0;
        LikeBadge.IsVisible = false;
        DislikeBadge.Opacity = 0;
        DislikeBadge.IsVisible = false;
    }
}
