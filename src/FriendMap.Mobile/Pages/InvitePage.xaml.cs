namespace FriendMap.Mobile.Pages;

public partial class InvitePage : ContentPage
{
    private string? _selectedPhone;
    private string? _selectedEmail;

    public InvitePage()
    {
        InitializeComponent();
        var code = GenerateInviteCode();
        InviteCodeLabel.Text = code;
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = Shell.Current.GoToAsync("..");
    }

    private void OnCopyCodeClicked(object? sender, EventArgs e)
    {
        Services.HapticService.Light();
        Clipboard.SetTextAsync(InviteCodeLabel.Text);
        Services.AnalyticsService.Invite("copy");
    }

    private async void OnShareInviteClicked(object? sender, EventArgs e)
    {
        await Services.ShareService.InviteAsync(InviteCodeLabel.Text);
    }

    private async void OnPickContactClicked(object? sender, EventArgs e)
    {
        try
        {
            var contact = await Microsoft.Maui.ApplicationModel.Communication.Contacts.PickContactAsync();
            if (contact is null) return;

            _selectedPhone = contact.Phones?.FirstOrDefault()?.PhoneNumber;
            _selectedEmail = contact.Emails?.FirstOrDefault()?.EmailAddress;
            SelectedContactLabel.Text = $"{contact.DisplayName} ({_selectedPhone ?? _selectedEmail ?? "no contact"})";
            InviteContactButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedPhone) || !string.IsNullOrWhiteSpace(_selectedEmail);
            Services.AnalyticsService.Invite("contact_picked");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Errore", $"Impossibile accedere ai contatti: {ex.Message}", "OK");
        }
    }

    private async void OnInviteContactClicked(object? sender, EventArgs e)
    {
        var message = $"Unisciti a me su FriendMap! Codice: {InviteCodeLabel.Text}";
        if (!string.IsNullOrWhiteSpace(_selectedPhone))
        {
            await Microsoft.Maui.ApplicationModel.Communication.Sms.ComposeAsync(new Microsoft.Maui.ApplicationModel.Communication.SmsMessage(message, _selectedPhone));
        }
        else if (!string.IsNullOrWhiteSpace(_selectedEmail))
        {
            await Microsoft.Maui.ApplicationModel.Communication.Email.ComposeAsync(new Microsoft.Maui.ApplicationModel.Communication.EmailMessage
            {
                Subject = "Invito FriendMap",
                Body = message,
                To = new List<string> { _selectedEmail }
            });
        }
        Services.AnalyticsService.Invite("sms_or_email");
    }
}
