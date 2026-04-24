namespace FriendMap.Mobile.Services;

public static class HapticService
{
    public static void Light()
    {
        try { HapticFeedback.Perform(HapticFeedbackType.Click); } catch { /* ignore */ }
    }

    public static void Medium()
    {
        try { HapticFeedback.Perform(HapticFeedbackType.LongPress); } catch { /* ignore */ }
    }

    public static void Heavy()
    {
        try
        {
            if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                // On iOS we can attempt a stronger haptic via custom pattern if available,
                // but LongPress is the strongest cross-platform fallback.
                HapticFeedback.Perform(HapticFeedbackType.LongPress);
            }
            else
            {
                HapticFeedback.Perform(HapticFeedbackType.LongPress);
            }
        }
        catch { /* ignore */ }
    }

    public static void Success()
    {
        try { HapticFeedback.Perform(HapticFeedbackType.LongPress); } catch { /* ignore */ }
    }

    public static void Error()
    {
        try { HapticFeedback.Perform(HapticFeedbackType.LongPress); } catch { /* ignore */ }
    }
}
