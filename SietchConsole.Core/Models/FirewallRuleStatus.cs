namespace SietchConsole.Core.Models;

public sealed record FirewallRuleStatus(BattlegroupPort Port, bool InboundExists)
{
    public bool IsConfigured => InboundExists;
}
