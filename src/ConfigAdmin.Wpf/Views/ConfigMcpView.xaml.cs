namespace ConfigAdmin.Wpf.Views;

public partial class ConfigMcpView : System.Windows.Controls.UserControl
{
    public ConfigMcpView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ConfigMcpViewModel vm)
            await vm.InitializeAsync();
    }
}
