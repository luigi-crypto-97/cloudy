using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace FriendMap.Mobile.Pages;

public partial class SocialTablePage : ContentPage, IQueryAttributable
{
    private readonly ApiClient _apiClient;
    private Guid _tableId;
    private SocialTableThread? _thread;

    public SocialTablePage(ApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        RequestsTitleLabel.IsVisible = false;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("tableId", out var value) &&
            Guid.TryParse(Uri.UnescapeDataString(value?.ToString() ?? string.Empty), out var tableId))
        {
            _tableId = tableId;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_tableId != Guid.Empty)
        {
            await LoadThreadAsync();
        }
    }

    private async Task LoadThreadAsync()
    {
        try
        {
            _thread = await _apiClient.GetTableThreadAsync(_tableId);
            var table = _thread.Table;
            TitleLabel.Text = table.Title;
            MetaLabel.Text = $"{table.VenueName} • {table.StartsAtUtc.ToLocalTime():ddd d MMM HH:mm}";
            DescriptionLabel.Text = table.Description ?? string.Empty;
            DescriptionLabel.IsVisible = !string.IsNullOrWhiteSpace(table.Description);
            CapacityLabel.Text = table.Capacity.ToString();
            AcceptedLabel.Text = table.AcceptedCount.ToString();
            PolicyLabel.Text = table.JoinPolicy == "auto" ? "Aperto" : "Approva";

            RenderRequests(table, _thread.Requests);
            RenderMessages(_thread.Messages);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Tavolo", _apiClient.DescribeException(ex), "OK");
        }
    }

    private void RenderRequests(SocialTableSummary table, IEnumerable<SocialTableRequest> requests)
    {
        RequestsLayout.Children.Clear();
        var items = requests.ToList();
        var showRequests = table.IsHost || items.Count > 0;
        RequestsTitleLabel.IsVisible = showRequests;
        RequestsLayout.IsVisible = showRequests;

        if (!showRequests)
        {
            return;
        }

        if (items.Count == 0)
        {
            RequestsLayout.Children.Add(new Label
            {
                Text = "Nessuna richiesta pendente.",
                FontSize = 14,
                TextColor = Color.FromArgb("#64748B")
            });
            return;
        }

        foreach (var request in items)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) }, ColumnSpacing = 8 };
            row.Add(new Label
            {
                Text = request.DisplayName ?? request.Nickname,
                VerticalOptions = LayoutOptions.Center,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#0F172A")
            });

            if (table.IsHost)
            {
                var approve = new Button
                {
                    Text = "OK",
                    BackgroundColor = Color.FromArgb("#10B981"),
                    TextColor = Colors.White,
                    HeightRequest = 36,
                    CornerRadius = 16,
                    Padding = new Thickness(12, 0)
                };
                approve.Clicked += async (_, _) =>
                {
                    await _apiClient.ApproveTableRequestAsync(table.TableId, request.UserId);
                    await LoadThreadAsync();
                };
                row.Add(approve, 1);

                var reject = new Button
                {
                    Text = "No",
                    BackgroundColor = Color.FromArgb("#E2E8F0"),
                    TextColor = Color.FromArgb("#334155"),
                    HeightRequest = 36,
                    CornerRadius = 16,
                    Padding = new Thickness(12, 0)
                };
                reject.Clicked += async (_, _) =>
                {
                    await _apiClient.RejectTableRequestAsync(table.TableId, request.UserId);
                    await LoadThreadAsync();
                };
                row.Add(reject, 2);
            }

            RequestsLayout.Children.Add(row);
        }
    }

    private void RenderMessages(IEnumerable<SocialTableMessage> messages)
    {
        MessagesLayout.Children.Clear();
        foreach (var message in messages)
        {
            var bubble = new Border
            {
                BackgroundColor = message.IsMine ? Color.FromArgb("#4F46E5") : Colors.White,
                Stroke = message.IsMine ? Colors.Transparent : Color.FromArgb("#E2E8F0"),
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                Padding = new Thickness(12, 10),
                HorizontalOptions = message.IsMine ? LayoutOptions.End : LayoutOptions.Start,
                Content = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        new Label
                        {
                            Text = message.DisplayName ?? message.Nickname,
                            FontSize = 11,
                            TextColor = message.IsMine ? Color.FromArgb("#C7D2FE") : Color.FromArgb("#64748B")
                        },
                        new Label
                        {
                            Text = message.Body,
                            FontSize = 14,
                            TextColor = message.IsMine ? Colors.White : Color.FromArgb("#0F172A")
                        }
                    }
                }
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
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var body = MessageEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(body) || _thread is null)
        {
            return;
        }

        try
        {
            await _apiClient.SendTableMessageAsync(_thread.Table.TableId, body);
            MessageEntry.Text = string.Empty;
            await LoadThreadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Tavolo", _apiClient.DescribeException(ex), "OK");
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
