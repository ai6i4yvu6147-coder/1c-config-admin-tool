using System.Windows;
using System.Windows.Controls;
using ConfigAdmin.Wpf.ViewModels;

namespace ConfigAdmin.Wpf.Views;

public partial class SyncAgentView
{
    public SyncAgentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is SyncAgentViewModel vm && string.IsNullOrEmpty(vm.PairingPassword))
            PairingPasswordBox.Password = string.Empty;
    }
}
