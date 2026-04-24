using FriendMap.Mobile.Services;

namespace FriendMap.Mobile.Pages;

public partial class CreateTablePage : ContentPage, IQueryAttributable
{
    private readonly ApiClient _apiClient;
    private Guid _venueId;
    private string _venueName = string.Empty;
    private string _venueCategory = string.Empty;

    public CreateTablePage(ApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        JoinPolicyPicker.ItemsSource = new[] { "Aperto a tutti", "Su approvazione" };
        JoinPolicyPicker.SelectedIndex = 1;
        StartDatePicker.Date = DateTime.Today;
        StartTimePicker.Time = DateTime.Now.AddHours(1).TimeOfDay;
        UpdateCapacityLabel();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("venueId", out var venueValue) &&
            Guid.TryParse(Uri.UnescapeDataString(venueValue?.ToString() ?? string.Empty), out var venueId))
        {
            _venueId = venueId;
        }

        if (query.TryGetValue("venueName", out var venueName))
        {
            _venueName = Uri.UnescapeDataString(venueName?.ToString() ?? string.Empty);
        }

        if (query.TryGetValue("venueCategory", out var venueCategory))
        {
            _venueCategory = Uri.UnescapeDataString(venueCategory?.ToString() ?? string.Empty);
        }

        VenueLabel.Text = _venueName;
        VenueMetaLabel.Text = _venueCategory;
        TitleEntry.Text = string.IsNullOrWhiteSpace(_venueName) ? string.Empty : $"Tavolo da {_venueName}";
    }

    private void OnCapacityChanged(object? sender, ValueChangedEventArgs e) => UpdateCapacityLabel();

    private void UpdateCapacityLabel()
    {
        CapacityLabel.Text = ((int)Math.Round(CapacityStepper.Value)).ToString();
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        if (_venueId == Guid.Empty)
        {
            SetStatus("Seleziona prima un locale dalla mappa.", true);
            return;
        }

        var title = TitleEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            SetStatus("Inserisci un titolo.", true);
            return;
        }

        try
        {
            var userId = await _apiClient.GetCurrentUserIdAsync();
            var startsAtLocal = StartDatePicker.Date + StartTimePicker.Time;
            var startsAt = new DateTimeOffset(startsAtLocal, TimeZoneInfo.Local.GetUtcOffset(startsAtLocal));
            var joinPolicy = JoinPolicyPicker.SelectedIndex == 0 ? "auto" : "approval";

            await _apiClient.CreateSocialTableAsync(
                userId,
                _venueId,
                title,
                string.IsNullOrWhiteSpace(DescriptionEditor.Text) ? null : DescriptionEditor.Text.Trim(),
                startsAt,
                (int)Math.Round(CapacityStepper.Value),
                joinPolicy);

            await DisplayAlert("Tavolo", "Tavolo creato.", "OK");
            await Shell.Current.GoToAsync("//main/social");
        }
        catch (Exception ex)
        {
            SetStatus(_apiClient.DescribeException(ex), true);
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void SetStatus(string message, bool isError)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = isError ? Color.FromArgb("#B91C1C") : Color.FromArgb("#4F46E5");
        StatusLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
    }
}
