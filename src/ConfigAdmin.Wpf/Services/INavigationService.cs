namespace ConfigAdmin.Wpf.Services;

public interface INavigationService
{
    event Action? NavigationChanged;

    bool CanGoBack { get; }

    void Attach(System.Windows.Controls.ContentControl host);
    void SetRoot(object viewModel);
    void SetRoot<TViewModel>() where TViewModel : class;
    void NavigateTo<TViewModel>() where TViewModel : class;
    void NavigateTo(object viewModel);
    void GoBack();
}
