using CommunityToolkit.Mvvm.ComponentModel;

namespace Sietch_Console.ViewModels.Steps;

public partial class TokenStepViewModel : ObservableObject
{
    public string Title => "Token";

    private const int MaxTokenLength = 2048;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceed))]
    [NotifyPropertyChangedFor(nameof(TokenError))]
    private string _token = string.Empty;

    // #180 — Trim whitespace on input and enforce a sane max length
    partial void OnTokenChanged(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > MaxTokenLength)
            trimmed = trimmed[..MaxTokenLength];

        if (trimmed != value)
        {
            Token = trimmed; // triggers another call, which will be stable
        }
    }

    // #180 — Inline validation error
    public string? TokenError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Token)) return null;
            if (Token.Length < 32)
                return "Token looks too short — check that you copied the full token.";
            return null;
        }
    }

    public bool CanProceed => !string.IsNullOrWhiteSpace(Token) && TokenError is null;
}
