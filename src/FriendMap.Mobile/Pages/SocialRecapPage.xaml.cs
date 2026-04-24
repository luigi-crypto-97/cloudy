using FriendMap.Mobile.Models;
using FriendMap.Mobile.Services;

namespace FriendMap.Mobile.Pages;

public partial class SocialRecapPage : ContentPage
{
    private readonly ApiClient _apiClient;

    public SocialRecapPage(ApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        PeriodPicker.ItemsSource = new[] { "Mese", "Anno" };
        PeriodPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var period = PeriodPicker.SelectedIndex == 1 ? "year" : "month";
            var recap = await _apiClient.GetMyRecapAsync(period);
            CheckInsLabel.Text = recap.TotalCheckIns.ToString();
            VenuesLabel.Text = recap.UniqueVenues.ToString();
            RenderVenues(recap.TopVenues);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Recap", _apiClient.DescribeException(ex), "OK");
        }
    }

    private void RenderVenues(IEnumerable<VenueRecapItem> venues)
    {
        TopVenuesLayout.Children.Clear();
        foreach (var venue in venues)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Star),
                    new(GridLength.Auto)
                }
            };

            row.Add(new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label
                    {
                        Text = venue.Name,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#0F172A")
                    },
                    new Label
                    {
                        Text = venue.Category,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#64748B")
                    }
                }
            });

            row.Add(new Label
            {
                Text = $"{venue.Visits}x",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#4F46E5"),
                VerticalOptions = LayoutOptions.Center
            }, 1);

            TopVenuesLayout.Children.Add(row);
        }

        if (!TopVenuesLayout.Children.Any())
        {
            TopVenuesLayout.Children.Add(new Label
            {
                Text = "Ancora nessun recap disponibile.",
                FontSize = 14,
                TextColor = Color.FromArgb("#64748B")
            });
        }
    }

    private async void OnPeriodChanged(object? sender, EventArgs e)
    {
        await LoadAsync();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
