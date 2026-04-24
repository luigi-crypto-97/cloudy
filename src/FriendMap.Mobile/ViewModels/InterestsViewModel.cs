using System.Collections.ObjectModel;
using System.Windows.Input;
using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;

namespace FriendMap.Mobile.ViewModels;

public class InterestsViewModel : BindableObject
{
    private readonly ApiClient _apiClient;
    private bool _isBusy;
    private int _currentIndex;
    private string? _statusMessage;

    public ObservableCollection<InterestCard> Cards { get; } = new();
    public List<string> SelectedTags { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public int CurrentIndex
    {
        get => _currentIndex;
        set { _currentIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); OnPropertyChanged(nameof(IsComplete)); }
    }

    public string ProgressText => $"{CurrentIndex + 1}/{Cards.Count}";
    public bool IsComplete => CurrentIndex >= Cards.Count;

    public string? StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatusMessage)); }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ICommand LikeCommand { get; }
    public ICommand DislikeCommand { get; }
    public ICommand SaveCommand { get; }

    public InterestsViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        LikeCommand = new Command(() => Swipe(true));
        DislikeCommand = new Command(() => Swipe(false));
        SaveCommand = new Command(async () => await SaveAsync());
        LoadDefaultCards();
    }

    private void LoadDefaultCards()
    {
        var defaults = new[]
        {
            new InterestCard { Tag = "cocktail", Emoji = "🍹", Description = "Aperitivi e drink artigianali" },
            new InterestCard { Tag = "live-music", Emoji = "🎸", Description = "Concerti, live e jam session" },
            new InterestCard { Tag = "dj-set", Emoji = "🎧", Description = "Serate DJ e house music" },
            new InterestCard { Tag = "karaoke", Emoji = "🎤", Description = "Cantare con gli amici" },
            new InterestCard { Tag = "beer", Emoji = "🍺", Description = "Birre artigianali e pub" },
            new InterestCard { Tag = "wine", Emoji = "🍷", Description = "Vini e degustazioni" },
            new InterestCard { Tag = "food", Emoji = "🍕", Description = "Street food e ristoranti" },
            new InterestCard { Tag = "sports", Emoji = "⚽", Description = "Guardare partite insieme" },
            new InterestCard { Tag = "gaming", Emoji = "🎮", Description = "Bar con giochi e arcade" },
            new InterestCard { Tag = "techno", Emoji = "🪩", Description = "Serate techno e underground" },
            new InterestCard { Tag = "latino", Emoji = "💃", Description = "Salsa, bachata e reggaeton" },
            new InterestCard { Tag = "hip-hop", Emoji = "🎤", Description = "Rap e hip-hop nights" },
            new InterestCard { Tag = "lgbtq", Emoji = "🌈", Description = "Eventi LGBTQ+ friendly" },
            new InterestCard { Tag = "student", Emoji = "🎓", Description = "Serate per studenti" },
            new InterestCard { Tag = "networking", Emoji = "🤝", Description = "Incontra persone nuove" },
        };

        foreach (var c in defaults)
            Cards.Add(c);
    }

    public void Swipe(bool liked)
    {
        if (CurrentIndex >= Cards.Count) return;
        var card = Cards[CurrentIndex];
        card.IsSelected = liked;
        if (liked)
            SelectedTags.Add(card.Tag);
        CurrentIndex++;
        HapticService.Light();
    }

    public async Task SaveAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            var profile = await _apiClient.GetMyProfileAsync();
            await _apiClient.UpdateMyProfileAsync(
                profile.DisplayName,
                profile.AvatarUrl,
                profile.Bio,
                profile.BirthYear,
                profile.Gender,
                SelectedTags);
            HapticService.Success();
            await Shell.Current.GoToAsync("//main");
        }
        catch (Exception ex)
        {
            StatusMessage = _apiClient.DescribeException(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
