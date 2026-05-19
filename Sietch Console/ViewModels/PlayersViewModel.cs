using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.ViewModels;

public enum PendingPlayerAction { None, Kick, Ban, Unban, RemoveAllowlist }

/// <summary>
/// Backs the Players view — connected players, ban list, and allowlist (#158, #159, #160).
/// </summary>
public partial class PlayersViewModel : ObservableObject
{
    private readonly IPlayerManagementService _players;

    // ── Tab state ─────────────────────────────────────────────────────────────

    [ObservableProperty] private int _selectedTabIndex;

    [RelayCommand]
    private void SelectTab(int index) => SelectedTabIndex = index;

    // ── Connected players ─────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConnectedPlayers))]
    private ObservableCollection<PlayerInfo> _connectedPlayers = [];

    public bool HasConnectedPlayers => ConnectedPlayers.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPlayer))]
    private PlayerInfo? _selectedPlayer;

    public bool HasSelectedPlayer => SelectedPlayer is not null;

    // ── Ban list ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBans))]
    private ObservableCollection<BanRecord> _bans = [];

    public bool HasBans => Bans.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedBan))]
    private BanRecord? _selectedBan;

    public bool HasSelectedBan => SelectedBan is not null;

    // ── Allowlist ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAllowlistEntries))]
    private ObservableCollection<AllowlistEntry> _allowlist = [];

    public bool HasAllowlistEntries => Allowlist.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedAllowlistEntry))]
    private AllowlistEntry? _selectedAllowlistEntry;

    public bool HasSelectedAllowlistEntry => SelectedAllowlistEntry is not null;

    // ── Add-to-allowlist form ─────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddToAllowlist))]
    private bool _showAddAllowlistForm;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddToAllowlist))]
    private string _newSteamId = string.Empty;

    [ObservableProperty] private string _newDisplayName = string.Empty;

    public bool CanAddToAllowlist =>
        ShowAddAllowlistForm && !string.IsNullOrWhiteSpace(NewSteamId);

    // ── Ban form ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBan))]
    private bool _showBanForm;

    [ObservableProperty] private string _banReason       = string.Empty;
    [ObservableProperty] private bool   _isPermanentBan  = true;
    [ObservableProperty] private int    _banDurationDays = 7;

    public bool CanBan => ShowBanForm && SelectedPlayer is not null;

    // ── Confirmation ──────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConfirmation))]
    [NotifyPropertyChangedFor(nameof(ConfirmationTitle))]
    [NotifyPropertyChangedFor(nameof(ConfirmationBody))]
    private PendingPlayerAction _pendingAction;

    private int    _pendingActionTargetId;
    private string _pendingActionTargetName = string.Empty;

    public bool   ShowConfirmation  => PendingAction != PendingPlayerAction.None;
    public string ConfirmationTitle => PendingAction switch
    {
        PendingPlayerAction.Kick            => "Kick Player?",
        PendingPlayerAction.Unban           => "Remove Ban?",
        PendingPlayerAction.RemoveAllowlist => "Remove from Allowlist?",
        _                                   => string.Empty,
    };
    public string ConfirmationBody => PendingAction switch
    {
        PendingPlayerAction.Kick            => $"Kick \"{_pendingActionTargetName}\" from the server?",
        PendingPlayerAction.Unban           => $"Remove the ban for \"{_pendingActionTargetName}\"?",
        PendingPlayerAction.RemoveAllowlist => $"Remove \"{_pendingActionTargetName}\" from the allowlist?",
        _                                   => string.Empty,
    };

    // ── Shared ────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────────

    public PlayersViewModel(IPlayerManagementService players)
    {
        _players = players;
        _players.PlayersChanged += OnPlayersChanged;
        _ = LoadAsync();
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await RefreshConnectedPlayersAsync();
            await RefreshBanListAsync();
            await RefreshAllowlistAsync();
        }
        finally { IsLoading = false; }
    }

    private async Task RefreshConnectedPlayersAsync()
    {
        var list = await _players.GetConnectedPlayersAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            ConnectedPlayers.Clear();
            foreach (var p in list)
                ConnectedPlayers.Add(p);
        });
    }

    private async Task RefreshBanListAsync()
    {
        var list = await _players.GetBanListAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            Bans.Clear();
            foreach (var r in list)
                Bans.Add(r);
        });
    }

    private async Task RefreshAllowlistAsync()
    {
        var list = await _players.GetAllowlistAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            Allowlist.Clear();
            foreach (var e in list)
                Allowlist.Add(e);
        });
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void OnPlayersChanged(object? sender, EventArgs e)
        => _ = RefreshConnectedPlayersAsync();

    // ── Connected player commands ─────────────────────────────────────────────

    [RelayCommand]
    private void RequestKick()
    {
        if (SelectedPlayer is null) return;
        _pendingActionTargetName = SelectedPlayer.PlayerName;
        PendingAction = PendingPlayerAction.Kick;
    }

    [RelayCommand]
    private void OpenBanForm()
    {
        if (SelectedPlayer is null) return;
        BanReason      = string.Empty;
        IsPermanentBan = true;
        BanDurationDays = 7;
        ShowBanForm    = true;
    }

    [RelayCommand]
    private void CloseBanForm() => ShowBanForm = false;

    [RelayCommand(CanExecute = nameof(CanBan))]
    private async Task ConfirmBanAsync()
    {
        if (SelectedPlayer is null) return;

        IsLoading = true;
        ShowBanForm = false;
        StatusMessage = string.Empty;
        try
        {
            var duration = IsPermanentBan ? (TimeSpan?)null : TimeSpan.FromDays(BanDurationDays);
            await _players.BanPlayerAsync(
                SelectedPlayer.PlayerId, SelectedPlayer.PlayerName,
                string.IsNullOrWhiteSpace(BanReason) ? null : BanReason.Trim(),
                duration);

            StatusMessage = $"{SelectedPlayer.PlayerName} has been banned.";
            await RefreshBanListAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ban failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    // ── Ban list commands ─────────────────────────────────────────────────────

    [RelayCommand]
    private void RequestUnban()
    {
        if (SelectedBan is null) return;
        _pendingActionTargetId   = SelectedBan.Id;
        _pendingActionTargetName = SelectedBan.PlayerName;
        PendingAction = PendingPlayerAction.Unban;
    }

    // ── Allowlist commands ────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenAddAllowlistForm()
    {
        NewSteamId     = string.Empty;
        NewDisplayName = string.Empty;
        ShowAddAllowlistForm = true;
    }

    [RelayCommand]
    private void CloseAddAllowlistForm() => ShowAddAllowlistForm = false;

    [RelayCommand(CanExecute = nameof(CanAddToAllowlist))]
    private async Task ConfirmAddToAllowlistAsync()
    {
        IsLoading            = true;
        ShowAddAllowlistForm = false;
        StatusMessage        = string.Empty;
        try
        {
            await _players.AddToAllowlistAsync(
                NewSteamId.Trim(),
                string.IsNullOrWhiteSpace(NewDisplayName) ? null : NewDisplayName.Trim());
            StatusMessage = $"{NewSteamId.Trim()} added to the allowlist.";
            await RefreshAllowlistAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void RequestRemoveAllowlistEntry()
    {
        if (SelectedAllowlistEntry is null) return;
        _pendingActionTargetId   = SelectedAllowlistEntry.Id;
        _pendingActionTargetName = SelectedAllowlistEntry.DisplayName
                                ?? SelectedAllowlistEntry.SteamId;
        PendingAction = PendingPlayerAction.RemoveAllowlist;
    }

    // ── Confirmation commands ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task ConfirmActionAsync()
    {
        var action = PendingAction;
        PendingAction = PendingPlayerAction.None;
        IsLoading     = true;
        StatusMessage = string.Empty;

        try
        {
            switch (action)
            {
                case PendingPlayerAction.Kick:
                    if (SelectedPlayer is not null)
                    {
                        await _players.KickPlayerAsync(SelectedPlayer.PlayerId);
                        StatusMessage = $"{SelectedPlayer.PlayerName} was kicked.";
                    }
                    break;

                case PendingPlayerAction.Unban:
                    await _players.UnbanPlayerAsync(_pendingActionTargetId);
                    StatusMessage = $"Ban for \"{_pendingActionTargetName}\" removed.";
                    await RefreshBanListAsync();
                    break;

                case PendingPlayerAction.RemoveAllowlist:
                    await _players.RemoveFromAllowlistAsync(_pendingActionTargetId);
                    StatusMessage = $"\"{_pendingActionTargetName}\" removed from allowlist.";
                    await RefreshAllowlistAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Operation failed: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void CancelAction() => PendingAction = PendingPlayerAction.None;

    // ── Refresh commands ──────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading     = true;
        StatusMessage = string.Empty;
        try   { await LoadAsync(); }
        catch (Exception ex) { StatusMessage = $"Refresh failed: {ex.Message}"; }
        finally { IsLoading = false; }
    }
}
