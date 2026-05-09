using Sietch_Console.ViewModels.Steps;
using System.Windows;
using System.Windows.Controls;

namespace Sietch_Console.Views.Steps;

public partial class BattlegroupConfigStepView : UserControl
{
    public BattlegroupConfigStepView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AdminPasswordBox.PasswordChanged += OnPasswordChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is BattlegroupConfigStepViewModel vm)
            AdminPasswordBox.Password = vm.AdminPassword;
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is BattlegroupConfigStepViewModel vm)
            vm.AdminPassword = AdminPasswordBox.Password;
    }
}
