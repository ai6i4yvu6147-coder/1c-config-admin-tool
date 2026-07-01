using System.Windows.Input;

namespace ConfigAdmin.Wpf.ViewModels;

public abstract class BusyViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            var changed = _isBusy != value;
            SetProperty(ref _isBusy, value);
            if (changed)
                InvalidateCommands();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    protected void InvalidateCommands() =>
        CommandManager.InvalidateRequerySuggested();
}
