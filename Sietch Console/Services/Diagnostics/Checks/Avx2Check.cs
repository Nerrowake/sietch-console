using System.Runtime.Intrinsics.X86;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Diagnostics.Checks;

/// <summary>
/// Verifies that the host CPU supports AVX2 instructions, which are required by the
/// Dune: Awakening dedicated server (confirmed in Funcom's official hosting docs).
/// </summary>
public static class Avx2Check
{
    public static DiagnosticsResult Run()
    {
        bool supported = Avx2.IsSupported;

        return new DiagnosticsResult
        {
            CheckName        = "AVX2 CPU Support",
            Title            = supported ? "AVX2 Supported" : "AVX2 Not Supported",
            Severity         = supported ? "Pass" : "Failure",
            TechnicalMessage = $"System.Runtime.Intrinsics.X86.Avx2.IsSupported = {supported}",
            FriendlyMessage  = supported
                ? "Your CPU supports AVX2 instructions, which are required by the Dune: Awakening dedicated server."
                : "Your CPU does not support AVX2 instructions. The Dune: Awakening dedicated server will not run on this hardware.",
            RecommendedAction = supported ? null
                : "The server requires a CPU with AVX2 support. Most Intel CPUs since Haswell (2013) " +
                  "and AMD CPUs since the Ryzen 1000 series (2017) include AVX2. " +
                  "Check your CPU model at cpu-world.com to confirm.",
        };
    }
}
