using Microsoft.Maui.Media;

namespace FriendMap.Mobile.Pages;

public partial class EditProfilePage : ContentPage
{
    private readonly Services.ApiClient _apiClient;
    private Models.EditableUserProfile? _profile;

    public EditProfilePage(Services.ApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        GenderPicker.ItemsSource = new[] { "undisclosed", "female", "male", "non-binary" };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _profile = await _apiClient.GetMyProfileAsync();
            DisplayNameEntry.Text = _profile.DisplayName;
            AvatarUrlEntry.Text = _profile.AvatarUrl;
            BirthYearEntry.Text = _profile.BirthYear?.ToString();
            GenderPicker.SelectedItem = _profile.Gender;
            BioEditor.Text = _profile.Bio;
            InterestsEditor.Text = string.Join(", ", _profile.Interests);
            UpdateAvatarFallback();
        }
        catch (Exception ex)
        {
            SetStatus($"Errore caricamento: {ex.Message}", true);
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnAvatarUrlChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateAvatarFallback();
    }

    private void UpdateAvatarFallback()
    {
        var text = string.IsNullOrWhiteSpace(DisplayNameEntry.Text) ? "?" : DisplayNameEntry.Text;
        AvatarFallbackLabel.Text = text.Length > 0 ? char.ToUpper(text[0]).ToString() : "?";
    }

    private async void OnUploadPhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var file = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = "Scegli foto profilo" });
            if (file is null) return;
            var updated = await _apiClient.UploadMyAvatarAsync(file);
            AvatarUrlEntry.Text = updated.AvatarUrl;
            UpdateAvatarFallback();
            SetStatus("Foto caricata!", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Errore upload: {ex.Message}", true);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            var birthYear = int.TryParse(BirthYearEntry.Text?.Trim(), out var by) ? (int?)by : null;
            var interests = (InterestsEditor.Text ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var updated = await _apiClient.UpdateMyProfileAsync(
                DisplayNameEntry.Text?.Trim(),
                AvatarUrlEntry.Text?.Trim(),
                BioEditor.Text?.Trim(),
                birthYear,
                GenderPicker.SelectedItem?.ToString(),
                interests);

            SetStatus("Profilo aggiornato!", false);
            Services.AnalyticsService.TrackEvent("profile_updated");
        }
        catch (Exception ex)
        {
            SetStatus($"Errore: {ex.Message}", true);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = isError
            ? Color.FromArgb("#B91C1C")
            : Color.FromArgb("#6D28D9");
        StatusLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
    }
}
