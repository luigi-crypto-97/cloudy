namespace FriendMap.Mobile.Services;

public static class ShareService
{
    public static async Task ShareVenueAsync(string venueName, string venueId)
    {
        await Microsoft.Maui.ApplicationModel.DataTransfer.Share.RequestAsync(new ShareTextRequest
        {
            Title = venueName,
            Text = $"Scopri {venueName} su FriendMap!",
            Uri = $"friendmap://venue/{venueId}"
        });
        AnalyticsService.Share("venue");
    }

    public static async Task ShareTableAsync(string tableTitle, string tableId)
    {
        await Microsoft.Maui.ApplicationModel.DataTransfer.Share.RequestAsync(new ShareTextRequest
        {
            Title = tableTitle,
            Text = $"Unisciti al tavolo '{tableTitle}' su FriendMap!",
            Uri = $"friendmap://table/{tableId}"
        });
        AnalyticsService.Share("table");
    }

    public static async Task ShareProfileAsync(string displayName, string userId)
    {
        await Microsoft.Maui.ApplicationModel.DataTransfer.Share.RequestAsync(new ShareTextRequest
        {
            Title = displayName,
            Text = $"Aggiungi {displayName} su FriendMap!",
            Uri = $"friendmap://user/{userId}"
        });
        AnalyticsService.Share("profile");
    }

    public static async Task InviteAsync(string inviteCode)
    {
        await Microsoft.Maui.ApplicationModel.DataTransfer.Share.RequestAsync(new ShareTextRequest
        {
            Title = "Invita su FriendMap",
            Text = $"Scarica FriendMap e usa il codice '{inviteCode}' per connetterti con me!",
            Uri = "https://friendmap.app"
        });
        AnalyticsService.Invite("generic");
    }
}
