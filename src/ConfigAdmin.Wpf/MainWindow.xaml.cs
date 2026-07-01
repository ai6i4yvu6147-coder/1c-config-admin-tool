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
    }

    private void UpdateNavBar()
    {
        var canGoBack = _navigationService.CanGoBack;
        NavBar.Visibility = canGoBack ? Visibility.Visible : Visibility.Collapsed;
        BackButton.IsEnabled = canGoBack;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_navigationService.CanGoBack)
            return;

        _navigationService.GoBack();
        e.Handled = true;
    }

    private void GoBackClick(object sender, RoutedEventArgs e) => _navigationService.GoBack();
}
