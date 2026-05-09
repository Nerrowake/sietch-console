namespace SietchConsole.Core.Models;

public sealed record NetworkInfo(
    string? HostIp,
    string? HostAdapterName,
    string? VmIp,
    string? VmAdapterName);
