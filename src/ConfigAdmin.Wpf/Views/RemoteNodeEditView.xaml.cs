using System.Windows.Controls;
using ConfigAdmin.Wpf.ViewModels;

namespace ConfigAdmin.Wpf.Views;

public partial class RemoteNodeEditView
{
    public RemoteNodeEditView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => PairingPasswordBox.Password = string.Empty;
    }

    private void PairingPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is RemoteNodeEditViewModel vm)
            vm.PairingPassword = PairingPasswordBox.Password;
    }
}
