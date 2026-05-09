using Sietch_Console.ViewModels;
using System.Windows;

namespace Sietch_Console;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
