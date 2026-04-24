using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace FriendMap.Mobile.Pages;

public partial class SocialProfilePage : ContentPage, IQueryAttributable
{
    private readonly ApiClient _apiClient;
    private Guid _userId;
    private UserProfile? _profile;

    public SocialProfilePage(ApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("userId", out var value) &&
            Guid.TryParse(Uri.UnescapeDataString(value?.ToString() ?? string.Empty), out var userId))
        {
            _userId = userId;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_userId != Guid.Empty)
        {
            await LoadProfileAsync();
        }
    }

    private async Task LoadProfileAsync()
    {
        try
        {
            _profile = await _apiClient.GetUserProfileAsync(_userId);
            DisplayNameLabel.Text = _profile.DisplayName ?? _profile.Nickname;
            NicknameLabel.Text = $"@{_profile.Nickname}";
            BioLabel.Text = _profile.Bio ?? string.Empty;
            BioLabel.IsVisible = !string.IsNullOrWhiteSpace(_profile.Bio);
            AvatarFallbackLabel.Text = BuildInitials(_profile.DisplayName ?? _profile.Nickname);
            PresenceLabel.Text = _profile.StatusLabel;
            StatsLabel.Text = $"{_profile.FriendsCount} amici • {_profile.MutualFriendsCount} in comune";

            PrimaryActionButton.Text = _profile.RelationshipStatus switch
            {
                "friend" => "Gia amici",
                "pending_sent" => "Richiesta inviata",
                "pending_received" => "Accetta",
                _ => "Aggiungi"
            };
            PrimaryActionButton.IsEnabled = _profile.RelationshipStatus is "none" or "pending_received";
            MessageButton.IsEnabled = _profile.CanMessageDirectly;

            InterestsLayout.Children.Clear();
            InterestsCard.IsVisible = _profile.Interests.Count > 0;
            foreach (var tag in _profile.Interests)
            {
                InterestsLayout.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#EEF2FF"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 14 },
                    Padding = new Thickness(10, 6),
                    Margin = new Thickness(0, 0, 8, 8),
                    Content = new Label
                    {
                        Text = tag,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#3730A3")
                    }
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Profilo", _apiClient.DescribeException(ex), "OK");
        }
    }

    private async void OnPrimaryActionClicked(object? sender, EventArgs e)
    {
        if (_profile is null)
        {
            return;
        }

        try
        {
            if (_profile.RelationshipStatus == "pending_received")
            {
                await _apiClient.AcceptFriendRequestAsync(_profile.UserId);
            }
            else if (_profile.RelationshipStatus == "none")
            {
                await _apiClient.SendFriendRequestAsync(_profile.UserId);
            }

            await LoadProfileAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Profilo", _apiClient.DescribeException(ex), "OK");
        }
    }

    private async void OnMessageClicked(object? sender, EventArgs e)
    {
        if (_profile is null || !_profile.CanMessageDirectly)
        {
            return;
        }

        await Shell.Current.GoToAsync($"{nameof(SocialChatPage)}?userId={Uri.EscapeDataString(_profile.UserId.ToString())}");
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private static string BuildInitials(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "?";
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0][0].ToString().ToUpperInvariant();
        }

        return string.Concat(parts.Take(2).Select(x => char.ToUpperInvariant(x[0])));
    }
}
