#if IOS
using UIKit;

namespace FriendMap.Mobile;

public static partial class TabBarBadgeHelper
{
    public static partial void SetBadge(Page page, int count)
    {
        if (page.Handler?.PlatformView is not UIViewController vc)
            return;

        vc.TabBarItem.BadgeValue = count > 0 ? count.ToString() : null;
    }
}
#endif
