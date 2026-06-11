using ConfigAdmin.Application.Services;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf.ViewModels;

public sealed class VaultViewModel : ObservableObject
{
    private readonly VaultSessionService _vaultSessionService;
    private readonly INavigationService _navigationService;
    private string _masterPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isInitialized;

    public VaultViewModel(VaultSessionService vaultSessionService, INavigationService navigationService)
    {
        _vaultSessionService = vaultSessionService;
        _navigationService = navigationService;
        SubmitCommand = new RelayCommand(SubmitAsync);
        _ = LoadStateAsync();
    }

    public string MasterPassword
    {
        get => _masterPassword;
        set => SetProperty(ref _masterPassword, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsInitialized
    {
        get => _isInitialized;
        set => SetProperty(ref _isInitialized, value);
    }

    public string Title => IsInitialized ? "Открыть хранилище" : "Создать мастер-пароль";
    public string SubmitButtonText => IsInitialized ? "Открыть" : "Создать";

    public RelayCommand SubmitCommand { get; }

    public void LockVault() => _vaultSessionService.Lock();

    private async Task LoadStateAsync()
    {
        IsInitialized = await _vaultSessionService.CheckInitializedAsync();
        RaisePropertyChanged(nameof(Title));
        RaisePropertyChanged(nameof(SubmitButtonText));
    }

    private async Task SubmitAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(MasterPassword))
            {
                StatusMessage = "Введите мастер-пароль.";
                return;
            }

            if (!IsInitialized)
            {
                if (MasterPassword != ConfirmPassword)
                {
                    StatusMessage = "Пароли не совпадают.";
                    return;
                }

                await _vaultSessionService.InitializeAsync(MasterPassword);
                IsInitialized = true;
            }
            else
            {
                await _vaultSessionService.UnlockAsync(MasterPassword);
            }

            MasterPassword = string.Empty;
            ConfirmPassword = string.Empty;
            StatusMessage = string.Empty;
            _navigationService.SetRoot<MainViewModel>();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
