using System.Windows;
using System.Windows;
using ConfigAdmin.Wpf.ViewModels;

namespace ConfigAdmin.Wpf.Views;

public partial class VaultView : System.Windows.Controls.UserControl
{
    public VaultView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        MasterPasswordBox.Password = string.Empty;
        ConfirmPasswordBox.Password = string.Empty;
    }

    private void OnMasterPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is VaultViewModel vm)
            vm.MasterPassword = MasterPasswordBox.Password;
    }

    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is VaultViewModel vm)
            vm.ConfirmPassword = ConfirmPasswordBox.Password;
    }
}
