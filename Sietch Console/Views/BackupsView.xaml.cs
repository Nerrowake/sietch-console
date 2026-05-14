using System.Windows;
using System.Windows.Controls;
using Sietch_Console.ViewModels;

namespace Sietch_Console.Views;

public partial class BackupsView : UserControl
{
    public BackupsView()
    {
        InitializeComponent();
    }

    private void S3SecretKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is BackupsViewModel vm)
            vm.S3SecretKey = ((PasswordBox)sender).Password;
    }
}
