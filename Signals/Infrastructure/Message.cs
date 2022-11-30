namespace Signals.Infrastructure;

internal sealed class Message
{
    private const int Unused = -1;

    private int _version;

    public Message(LinkedListNode<ISource> source, LinkedListNode<ITarget> target)
    {
        SourceNode = source;
        TargetNode = target;
        Rollback = source.Value.Listener;
    }

    public LinkedListNode<ISource> SourceNode { get; }

    public LinkedListNode<ITarget> TargetNode { get; }

    public bool IsUnused => _version == Unused;

    public Message? Rollback { get; private set; }

    public IEnumerable<ITarget> Targets
    {
        get
        {
            for (var target = TargetNode; target is not null; target = target.Next)
            {
                yield return target.Value;
            }
        }
    }

    public IEnumerable<ISource> Sources
    {
        get
        {
            for (var source = SourceNode; source is not null; source = source.Next)
            {
                yield return source.Value;
            }
        }
    }

    public bool ShouldRefresh
    {
        get
        {
            var source = SourceNode.Value;

            if (_version != source.Listener?._version)
            {
                return true;
            }

            if (!source.Refresh())
            {
                return true;
            }

            if (_version != source.Listener?._version)
            {
                return true;
            }

            return false;
        }
    }

    public void Backup()
    {
        if (SourceNode.Value.Listener is Message rollback)
        {
            Rollback = rollback;
        }

        SourceNode.Value.Listener = this; // TODO
        _version = Unused;
    }

    public void Refresh()
    {
        SourceNode.Value.Track(this);
        TargetNode.Value.Watch(this);
        _version = 0;
    }

    public void SyncVersion() => _version = SourceNode.Value.Version;
}
