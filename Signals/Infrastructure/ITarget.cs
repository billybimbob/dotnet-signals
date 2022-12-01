namespace Signals.Infrastructure;

internal interface ITarget
{
    Status Status { get; }

    Message? Watching { get; set; }

    void Notify();
}
