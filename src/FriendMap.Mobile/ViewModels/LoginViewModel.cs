using System.Windows.Input;
using FriendMap.Mobile.Services;

namespace FriendMap.Mobile.ViewModels;

public class LoginViewModel : BindableObject
{
    private readonly ApiClient _apiClient;
    private string _nickname = "giulia";
    private string? _error;
    private bool _isBusy;

    public string Nickname
    {
        get => _nickname;
        set
        {
            _nickname = value;
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
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoginCommand { get; }

    public LoginViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        LoginCommand = new Command(async () => await LoginAsync());
    }

    public async Task<bool> TryRestoreAsync()
    {
        return await _apiClient.TryRestoreSessionAsync();
    }

    private async Task LoginAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;

        try
        {
            await _apiClient.DevLoginAsync(Nickname, Nickname);
            await Shell.Current.GoToAsync("//map");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
