namespace Signals.Infrastructure;

internal interface ITarget
{
    bool IsTracking { get; }

    Message? Watching { get; }

    void Watch(Message message);

    void Notify();
}
