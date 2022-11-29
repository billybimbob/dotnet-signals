namespace Signals.Infrastructure;

internal interface ISource
{
    int Version { get; }

    Message? Listener { get; }

    bool Refresh();

    void Track(Message message);

    void Untrack(Message message);
}
