using Microsoft.AppCenter.Analytics;

namespace FriendMap.Mobile.Services;

public static class AnalyticsService
{
    public static void TrackEvent(string name, IDictionary<string, string>? properties = null)
    {
        try
        {
            Analytics.TrackEvent(name, properties);
        }
        catch { /* App Center may not be initialized */ }
    }

    public static void Login(string method) => TrackEvent("login", new Dictionary<string, string> { ["method"] = method });
    public static void Logout() => TrackEvent("logout");
    public static void CheckIn(string venueId) => TrackEvent("check_in", new Dictionary<string, string> { ["venue_id"] = venueId });
    public static void SendMessage(string type) => TrackEvent("send_message", new Dictionary<string, string> { ["type"] = type });
    public static void FriendRequest(string action) => TrackEvent("friend_request", new Dictionary<string, string> { ["action"] = action });
    public static void OpenTable(string venueId) => TrackEvent("open_table", new Dictionary<string, string> { ["venue_id"] = venueId });
    public static void Share(string contentType) => TrackEvent("share", new Dictionary<string, string> { ["content_type"] = contentType });
    public static void Invite(string channel) => TrackEvent("invite", new Dictionary<string, string> { ["channel"] = channel });
    public static void ScreenView(string screen) => TrackEvent("screen_view", new Dictionary<string, string> { ["screen"] = screen });
}
