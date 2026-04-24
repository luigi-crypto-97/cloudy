using FriendMap.Mobile.Pages;

namespace FriendMap.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(OnboardingPage), typeof(OnboardingPage));
        Routing.RegisterRoute(nameof(InterestsPage), typeof(InterestsPage));
        Routing.RegisterRoute(nameof(MainMapPage), typeof(MainMapPage));
        Routing.RegisterRoute(nameof(SocialPage), typeof(SocialPage));
        Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
        Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
        Routing.RegisterRoute(nameof(PrivacyPage), typeof(PrivacyPage));
        Routing.RegisterRoute(nameof(TermsPage), typeof(TermsPage));
        Routing.RegisterRoute(nameof(EditProfilePage), typeof(EditProfilePage));
        Routing.RegisterRoute(nameof(StoriesPage), typeof(StoriesPage));
        Routing.RegisterRoute(nameof(DiscoveryPage), typeof(DiscoveryPage));
        Routing.RegisterRoute(nameof(GamificationPage), typeof(GamificationPage));
        Routing.RegisterRoute(nameof(InvitePage), typeof(InvitePage));
        Routing.RegisterRoute(nameof(SocialChatPage), typeof(SocialChatPage));
        Routing.RegisterRoute(nameof(SocialProfilePage), typeof(SocialProfilePage));
        Routing.RegisterRoute(nameof(SocialTablePage), typeof(SocialTablePage));
        Routing.RegisterRoute(nameof(SocialRecapPage), typeof(SocialRecapPage));
        Routing.RegisterRoute(nameof(CreateTablePage), typeof(CreateTablePage));
    }
}
