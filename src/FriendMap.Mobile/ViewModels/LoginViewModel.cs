using System.Windows.Input;
using FriendMap.Mobile.Pages;
using FriendMap.Mobile.Services;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace FriendMap.Mobile.ViewModels;

public class LoginViewModel : BindableObject
{
    private const string PermissionPrimerCompletedKey = "friendmap_permission_primer_completed";
    private readonly ApiClient _apiClient;
    private string _nickname = "giulia";
    private string _apiBaseUrl;
    private string? _backendStatusMessage;
    private Color _backendStatusColor = Color.FromArgb("#64748B");
    private string? _error;
    private bool _isBusy;
    private bool _skipAutoRestoreOnce;

    public string Nickname
    {
        get => _nickname;
        set
        {
            _nickname = value;
            OnPropertyChanged();
        }
    }

    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set
        {
            _apiBaseUrl = value;
            OnPropertyChanged();
        }
    }

    public string ConnectionHint => "Sim: 127.0.0.1:8080  |  iPhone: IP del Mac:8080";

    public string PushModeLabel => MobileBuildFeatures.PushNotificationsEnabled
        ? "Build con APNs"
        : "Build dev senza push";

    public string? BackendStatusMessage
    {
        get => _backendStatusMessage;
        set
        {
            _backendStatusMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasBackendStatusMessage));
        }
    }

    public bool HasBackendStatusMessage => !string.IsNullOrWhiteSpace(BackendStatusMessage);

    public Color BackendStatusColor
    {
        get => _backendStatusColor;
        set
        {
            _backendStatusColor = value;
            OnPropertyChanged();
        }
    }

    public string? Error
    {
        get => _error;
        set
        {
            _error = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEditSettings));
        }
    }

    public bool CanEditSettings => !IsBusy;

    public ICommand LoginCommand { get; }
    public ICommand VerifyBackendCommand { get; }

    public LoginViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        _apiBaseUrl = _apiClient.GetConfiguredApiBaseUrl();
        LoginCommand = new Command(async () => await LoginAsync());
        VerifyBackendCommand = new Command(async () => await VerifyBackendAsync());
    }

    public void PauseAutoRestoreOnce()
    {
        _skipAutoRestoreOnce = true;
    }

    public async Task<bool> TryRestoreAsync()
    {
        if (_skipAutoRestoreOnce)
        {
            _skipAutoRestoreOnce = false;
            return false;
        }

        var apiBaseUrl = _apiClient.GetConfiguredApiBaseUrl();
        if (DeviceInfo.Current.DeviceType == DeviceType.Physical &&
            (apiBaseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
             apiBaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)))
        {
            BackendStatusMessage = "Su iPhone usa l'IP del Mac, non localhost.";
            BackendStatusColor = Color.FromArgb("#B91C1C");
            return false;
        }

        _apiClient.ConfigureApiBaseUrl(apiBaseUrl);
        if (!await RefreshBackendStatusAsync(showSuccess: false))
        {
            return false;
        }

        return await _apiClient.TryRestoreSessionAsync();
    }

    public string ResolveAuthenticatedRoute()
    {
        return Preferences.Get(PermissionPrimerCompletedKey, false)
            ? "//main"
            : "//onboarding";
    }

    public static void MarkPermissionPrimerCompleted()
    {
        Preferences.Set(PermissionPrimerCompletedKey, true);
    }

    private async Task VerifyBackendAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;

        try
        {
            _apiClient.ConfigureApiBaseUrl(ApiBaseUrl);
            await RefreshBackendStatusAsync(showSuccess: true);
        }
        catch (Exception ex)
        {
            Error = _apiClient.DescribeException(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoginAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;

        try
        {
            _apiClient.ConfigureApiBaseUrl(ApiBaseUrl);
            if (!await RefreshBackendStatusAsync(showSuccess: true))
            {
                Error = "Backend non raggiungibile. Correggi il Backend URL prima del login.";
                return;
            }

            await _apiClient.DevLoginAsync(Nickname, Nickname);
            await Shell.Current.GoToAsync(ResolveAuthenticatedRoute());
        }
        catch (Exception ex)
        {
            Error = _apiClient.DescribeException(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> RefreshBackendStatusAsync(bool showSuccess)
    {
        try
        {
            var health = await _apiClient.GetHealthAsync();
            if (showSuccess)
            {
                BackendStatusMessage = $"Backend raggiungibile: {health.Status}.";
                BackendStatusColor = Color.FromArgb("#166534");
            }
            else
            {
                BackendStatusMessage = null;
            }

            return true;
        }
        catch (Exception ex)
        {
            BackendStatusMessage = _apiClient.DescribeException(ex);
            BackendStatusColor = Color.FromArgb("#B91C1C");
            return false;
        }
    }
}
