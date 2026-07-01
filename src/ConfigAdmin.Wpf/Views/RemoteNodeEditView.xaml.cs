using System.Windows.Controls;

namespace ConfigAdmin.Wpf.Views;

public partial class RemoteNodeEditView
{
    public RemoteNodeEditView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => PairingPasswordBox.Password = string.Empty;
    }
}
