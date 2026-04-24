using System.ComponentModel;
using FriendMap.Mobile.Services;
using FriendMap.Mobile.ViewModels;
using Microsoft.Maui.ApplicationModel;

namespace FriendMap.Mobile.Pages;

public partial class OnboardingPage : ContentPage, INotifyPropertyChanged
{
    private readonly IDevicePermissionService _permissions;
    private bool _isBusy;
    private PermissionStatus _locationStatus = PermissionStatus.Unknown;
    private PermissionStatus _contactsStatus = PermissionStatus.Unknown;
    private bool _pushEnabled;

    public string LocationStatusText => BuildPermissionStatusText(_locationStatus, "Posizione");
    public string PushStatusText => _pushEnabled ? "Notifiche attive" : "Notifiche non attive";
    public string ContactsStatusText => BuildPermissionStatusText(_contactsStatus, "Rubrica");
    public string ContinueButtonText => AllCorePermissionsReady ? "Apri Cloudy" : "Continua comunque";
    public bool AllCorePermissionsReady => _locationStatus == PermissionStatus.Granted && _contactsStatus == PermissionStatus.Granted && _pushEnabled;

    public OnboardingPage(IDevicePermissionService permissions)
    {
        InitializeComponent();
        _permissions = permissions;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshStatusesAsync();
    }

    private async void OnActivateLocationClicked(object? sender, EventArgs e)
    {
        if (_isBusy) return;
        await RunBusyAsync(async () =>
        {
            _locationStatus = await _permissions.RequestLocationAsync();
            await RefreshStatusesAsync();
        });
    }

    private async void OnActivatePushClicked(object? sender, EventArgs e)
    {
        if (_isBusy) return;
        await RunBusyAsync(async () =>
        {
            _pushEnabled = await _permissions.RequestPushNotificationsPermissionAsync();
            await RefreshStatusesAsync();
        });
    }

    private async void OnActivateContactsClicked(object? sender, EventArgs e)
    {
        if (_isBusy) return;
        await RunBusyAsync(async () =>
        {
            _contactsStatus = await _permissions.RequestContactsAsync();
            await RefreshStatusesAsync();
        });
    }

    private async void OnOpenSettingsClicked(object? sender, EventArgs e)
    {
        _permissions.OpenAppSettings();
        await DisplayAlert("Impostazioni", "Riabilita posizione, notifiche o contatti nelle impostazioni di iPhone, poi torna qui.", "OK");
    }

    private async void OnContinueClicked(object? sender, EventArgs e)
    {
        LoginViewModel.MarkPermissionPrimerCompleted();
        await Shell.Current.GoToAsync("//main");
    }

    private async Task RefreshStatusesAsync()
    {
        _locationStatus = await _permissions.GetLocationStatusAsync();
        _contactsStatus = await _permissions.GetContactsStatusAsync();
        _pushEnabled = await _permissions.GetPushNotificationsEnabledAsync();
        NotifyStatusBindings();
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        try
        {
            _isBusy = true;
            await action();
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void NotifyStatusBindings()
    {
        OnPropertyChanged(nameof(LocationStatusText));
        OnPropertyChanged(nameof(PushStatusText));
        OnPropertyChanged(nameof(ContactsStatusText));
        OnPropertyChanged(nameof(AllCorePermissionsReady));
        OnPropertyChanged(nameof(ContinueButtonText));
    }

    private static string BuildPermissionStatusText(PermissionStatus status, string label)
    {
        return status switch
        {
            PermissionStatus.Granted => $"{label} attiva",
            PermissionStatus.Denied => $"{label} negata",
            PermissionStatus.Restricted => $"{label} limitata",
            PermissionStatus.Disabled => $"{label} disattivata",
            _ => $"{label} non attiva"
        };
    }
}
