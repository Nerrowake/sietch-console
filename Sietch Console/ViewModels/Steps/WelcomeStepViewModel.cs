using CommunityToolkit.Mvvm.ComponentModel;

namespace Sietch_Console.ViewModels.Steps;

public partial class WelcomeStepViewModel : ObservableObject
{
    public string Title => "Welcome";
    public bool CanProceed => true;
}
