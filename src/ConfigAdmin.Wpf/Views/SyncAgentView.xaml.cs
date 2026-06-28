using System.Windows.Controls;
using ConfigAdmin.Wpf.ViewModels;

namespace ConfigAdmin.Wpf.Views;

public partial class SyncAgentView
{
    public SyncAgentView()
    {
        InitializeComponent();
    }

    private void PairingPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SyncAgentViewModel vm)
            vm.PairingPassword = PairingPasswordBox.Password;
    }
}
