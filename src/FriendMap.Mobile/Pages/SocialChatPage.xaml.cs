using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace FriendMap.Mobile.Pages;

public partial class SocialChatPage : ContentPage, IQueryAttributable
{
    private readonly ApiClient _apiClient;
    private Guid _userId;
    private DirectMessageThread? _thread;

    public SocialChatPage(ApiClient apiClient)
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
            await LoadThreadAsync();
        }
    }

    private async Task LoadThreadAsync()
    {
        try
        {
            HeaderLoading.IsVisible = true;
            HeaderLoading.IsRunning = true;
            _thread = await _apiClient.GetDirectMessageThreadAsync(_userId);
            TitleLabel.Text = _thread.OtherUser.DisplayName ?? _thread.OtherUser.Nickname;
            SubtitleLabel.Text = $"@{_thread.OtherUser.Nickname}";
            RenderMessages(_thread.Messages);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Chat", _apiClient.DescribeException(ex), "OK");
        }
        finally
        {
            HeaderLoading.IsVisible = false;
            HeaderLoading.IsRunning = false;
        }
    }

    private void RenderMessages(IEnumerable<DirectMessageItem> messages)
    {
        MessagesLayout.Children.Clear();

        foreach (var message in messages)
        {
            var text = new Label
            {
                Text = message.Body,
                FontSize = 15,
                TextColor = message.IsMine ? Colors.White : Color.FromArgb("#0F172A"),
                LineBreakMode = LineBreakMode.WordWrap
            };

            var meta = new Label
            {
                Text = message.SentAtUtc.ToLocalTime().ToString("HH:mm"),
                FontSize = 11,
                TextColor = message.IsMine ? Color.FromArgb("#C7D2FE") : Color.FromArgb("#64748B"),
                HorizontalTextAlignment = message.IsMine ? TextAlignment.End : TextAlignment.Start
            };

            var stack = new VerticalStackLayout
            {
                Spacing = 4,
                Children = { text, meta }
            };

            var bubble = new Border
            {
                BackgroundColor = message.IsMine ? Color.FromArgb("#4F46E5") : Colors.White,
                Stroke = message.IsMine ? Colors.Transparent : Color.FromArgb("#E2E8F0"),
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                Padding = new Thickness(14, 10),
                MaximumWidthRequest = 290,
                HorizontalOptions = message.IsMine ? LayoutOptions.End : LayoutOptions.Start,
                Content = stack
            };

            MessagesLayout.Children.Add(bubble);
        }

        if (!MessagesLayout.Children.Any())
        {
            MessagesLayout.Children.Add(new Label
            {
                Text = "Nessun messaggio ancora.",
                FontSize = 14,
                TextColor = Color.FromArgb("#64748B")
            });
        }

        if (_thread?.Messages.Count > 0)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(40);
                await MessagesScroll.ScrollToAsync(0, double.MaxValue, false);
            });
        }
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var body = MessageEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(body) || _userId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _apiClient.SendDirectMessageAsync(_userId, body);
            MessageEntry.Text = string.Empty;
            await LoadThreadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Chat", _apiClient.DescribeException(ex), "OK");
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
