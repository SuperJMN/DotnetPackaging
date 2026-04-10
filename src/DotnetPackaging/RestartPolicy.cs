namespace DotnetPackaging;

public enum RestartPolicy
{
    No,
    Always,
    OnFailure,
    OnAbnormal,
    OnAbort,
    OnWatchdog
}
