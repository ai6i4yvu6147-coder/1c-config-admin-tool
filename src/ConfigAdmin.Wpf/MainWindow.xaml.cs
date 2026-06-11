using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ConfigAdmin.Wpf.Services;

namespace ConfigAdmin.Wpf;

public partial class MainWindow : Window
{
    private readonly INavigationService _navigationService;

    public MainWindow(INavigationService navigationService)
    {
        _navigationService = navigationService;
        InitializeComponent();

        _navigationService.NavigationChanged += UpdateNavBar;
        UpdateNavBar();

        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosing;
    }

    private void UpdateNavBar()
    {
        NavBar.Visibility = _navigationService.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_navigationService.CanGoBack)
            return;

        _navigationService.GoBack();
        e.Handled = true;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_navigationService.CanGoBack)
            return;

        e.Cancel = true;
        _navigationService.GoBack();
    }

    private void GoBackClick(object sender, RoutedEventArgs e) => _navigationService.GoBack();
}
