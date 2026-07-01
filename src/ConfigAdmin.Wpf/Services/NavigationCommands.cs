using System.Windows.Input;
using ConfigAdmin.Wpf.ViewModels;

namespace ConfigAdmin.Wpf.Services;

public static class NavigationCommands
{
    public static RelayCommand Back(INavigationService navigationService) =>
        new(() => navigationService.GoBack());
}
