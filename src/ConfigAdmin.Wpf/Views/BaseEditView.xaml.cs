using System.Windows;
using ConfigAdmin.Wpf.ViewModels;

namespace ConfigAdmin.Wpf.Views;

public partial class BaseEditView
{
    public BaseEditView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public void ResetPasswordField() => PasswordBox.Password = string.Empty;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is BaseEditViewModel vm && string.IsNullOrEmpty(vm.Password))
            ResetPasswordField();
    }
}
