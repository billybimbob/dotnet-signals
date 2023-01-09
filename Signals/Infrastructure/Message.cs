namespace Signals.Infrastructure;

internal sealed class Message
{
    private const int Unused = -1;

    private int _version;
    private Link<Message>? _rollback;

    public Message(ISource source, ITarget target)
    {
        _version = Unused;
        _rollback = source.Listener;
        Source = source;
        Target = target;
    }

    public ISource Source { get; }

    public ITarget Target { get; }

    public bool IsUnused => _version == Unused;

    public bool IsOutdated => _version != Source.Version;

    public void SyncVersion() => _version = Source.Version;

    public void Utilize()
    {
        if (IsUnused)
        {
            _version = 0;
        }
    }

    public void Backup()
    {
        if (Source.Listener is Link<Message> rollback)
        {
            _rollback = rollback;
        }

        _version = Unused;
    }

    public void Restore()
    {
        Source.Listener = _rollback;
        _rollback = null;
    }
}
