namespace Signals.Infrastructure;

[Flags]
internal enum Status
{
    None = 0,
    Running = 1 << 0,
    Notified = 1 << 1,
    Outdated = 1 << 2,
    Disposed = 1 << 3,
    Tracking = 1 << 4
}
