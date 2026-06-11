using System.Windows;
using System.Windows;
using ConfigAdmin.Wpf.ViewModels;

namespace ConfigAdmin.Wpf.Views;

public partial class BaseEditView : System.Windows.Controls.UserControl
{
    public BaseEditView()
    {
        InitializeComponent();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is BaseEditViewModel vm)
            vm.Password = PasswordBox.Password;
    }
}
