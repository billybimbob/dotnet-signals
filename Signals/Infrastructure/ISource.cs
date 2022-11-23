namespace Signals.Infrastructure;

internal interface ISource
{
    int Version { get; }

    Message? Current { get; }

    Message? Listener { get; }

    void Track(Message message);

    void Untrack(Message message);

    bool Refresh();
}
