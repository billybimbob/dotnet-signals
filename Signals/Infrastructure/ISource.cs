namespace Signals.Infrastructure;

internal interface ISource
{
    int Version { get; }

    Link<Message>? Listener { get; set; }

    bool Update();

    void Track(Link<Message> link);

    void Untrack(Link<Message> link);
}
