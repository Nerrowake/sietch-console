using CommunityToolkit.Mvvm.ComponentModel;

namespace Sietch_Console.ViewModels.Steps;

public partial class TokenStepViewModel : ObservableObject
{
    public string Title => "Token";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    private string _token = string.Empty;

    public bool CanProceed => !string.IsNullOrWhiteSpace(Token);
}
