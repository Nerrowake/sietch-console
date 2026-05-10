namespace SietchConsole.Core.Exceptions;

public enum HyperVErrorCode
{
    Unknown,
    HyperVNotInstalled,
    AccessDenied,
    VmNotFound,
    VmAlreadyRunning,
    VmAlreadyStopped,
    VmLocked,
    ProvisioningFailed,
    OperationTimedOut,
    WmiQueryFailed,
}

public class HyperVException : Exception
{
    public HyperVErrorCode ErrorCode { get; }

    public HyperVException(HyperVErrorCode code, string message, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = code;
    }

    public string UserFacingMessage => ErrorCode switch
    {
        HyperVErrorCode.HyperVNotInstalled => "Hyper-V is not installed or enabled. Enable it in Windows Features and restart.",
        HyperVErrorCode.AccessDenied       => "Access denied. Run Sietch Console as Administrator to manage Hyper-V.",
        HyperVErrorCode.VmNotFound         => "The configured VM was not found. Check the VM name in your battlegroup profile.",
        HyperVErrorCode.VmAlreadyRunning   => "The VM is already running.",
        HyperVErrorCode.VmAlreadyStopped   => "The VM is already stopped.",
        HyperVErrorCode.VmLocked           => "The VM is in a locked or transitioning state. Wait a moment and try again.",
        HyperVErrorCode.ProvisioningFailed => "Failed to create the virtual machine. Check Hyper-V Manager for details.",
        HyperVErrorCode.OperationTimedOut  => "The operation timed out. The VM may still be transitioning — check Hyper-V Manager.",
        HyperVErrorCode.WmiQueryFailed     => "Failed to communicate with Hyper-V. Ensure the Virtual Machine Management Service (VMMS) is running.",
        _                                  => Message,
    };
}
