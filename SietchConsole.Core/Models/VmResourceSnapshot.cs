namespace SietchConsole.Core.Models;

/// <summary>Point-in-time CPU and memory utilization for a running Hyper-V VM.</summary>
public record VmResourceSnapshot(
    int  CpuPercent,  // 0–100
    long MemoryMb);   // Current memory demand reported by Hyper-V
