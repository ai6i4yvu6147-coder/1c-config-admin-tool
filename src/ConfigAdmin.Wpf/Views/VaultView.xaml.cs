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
}
