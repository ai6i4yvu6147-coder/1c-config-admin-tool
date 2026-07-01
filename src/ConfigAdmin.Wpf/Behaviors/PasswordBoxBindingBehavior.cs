using System.Windows;
using System.Windows.Controls;

namespace ConfigAdmin.Wpf.Behaviors;

public static class PasswordBoxBindingBehavior
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxBindingBehavior),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxBindingBehavior));

    public static string GetBoundPassword(DependencyObject element) =>
        (string)element.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject element, string value) =>
        element.SetValue(BoundPasswordProperty, value);

    public static readonly DependencyProperty AttachProperty =
        DependencyProperty.RegisterAttached(
            "Attach",
            typeof(bool),
            typeof(PasswordBoxBindingBehavior),
            new PropertyMetadata(false, OnAttachChanged));

    public static bool GetAttach(DependencyObject element) =>
        (bool)element.GetValue(AttachProperty);

    public static void SetAttach(DependencyObject element, bool value) =>
        element.SetValue(AttachProperty, value);

    private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox)
            return;

        if ((bool)e.NewValue)
            passwordBox.PasswordChanged += OnPasswordChanged;
        else
            passwordBox.PasswordChanged -= OnPasswordChanged;
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox || (bool)passwordBox.GetValue(IsUpdatingProperty))
            return;

        passwordBox.Password = e.NewValue as string ?? string.Empty;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
            return;

        passwordBox.SetValue(IsUpdatingProperty, true);
        SetBoundPassword(passwordBox, passwordBox.Password);
        passwordBox.SetValue(IsUpdatingProperty, false);
    }
}
