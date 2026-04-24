using FriendMap.Mobile.Services;
using Microsoft.Maui.Devices.Sensors;

namespace FriendMap.Mobile.Pages;

public partial class MainMapPage
{
    private void StartBumpAccelerometer()
    {
        if (!Accelerometer.IsSupported) return;
        try
        {
            Accelerometer.ReadingChanged += OnAccelerometerReadingChanged;
            Accelerometer.Start(SensorSpeed.Game);
        }
        catch { /* ignore */ }
    }

    private void StopBumpAccelerometer()
    {
        if (!Accelerometer.IsSupported) return;
        try
        {
            Accelerometer.ReadingChanged -= OnAccelerometerReadingChanged;
            Accelerometer.Stop();
        }
        catch { /* ignore */ }
    }

    private void OnAccelerometerReadingChanged(object? sender, AccelerometerChangedEventArgs e)
    {
        var accel = e.Reading.Acceleration;
        var magnitude = Math.Sqrt(accel.X * accel.X + accel.Y * accel.Y + accel.Z * accel.Z);
        if (magnitude > 2.2 && !_isShaking && (DateTimeOffset.UtcNow - _lastShakeUtc).TotalSeconds > 2)
        {
            _isShaking = true;
            _lastShakeUtc = DateTimeOffset.UtcNow;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                HapticService.Heavy();
                _viewModel.SetStatusMessage("⚡ Bump! Scopri chi c'è nelle vicinanze!");
                await BumpFab.ScaleTo(1.3, 150, Easing.SpringOut);
                await BumpFab.ScaleTo(1, 150, Easing.SpringOut);
                _isShaking = false;
            });
        }
    }

    private async Task ShowSuccessOverlayAsync(string message)
    {
        SuccessLabel.Text = message;
        SuccessOverlay.Opacity = 0;
        SuccessOverlay.IsVisible = true;
        SuccessLottie.IsAnimationEnabled = true;
        await SuccessOverlay.FadeTo(1, 220, Easing.CubicOut);
        await Task.Delay(1600);
        await SuccessOverlay.FadeTo(0, 280, Easing.CubicIn);
        SuccessOverlay.IsVisible = false;
        SuccessLottie.IsAnimationEnabled = false;
    }
}
