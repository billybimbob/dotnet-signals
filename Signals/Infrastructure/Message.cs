namespace Signals.Infrastructure;

internal sealed class Message
{
    private const int Unused = -1;

    private int _version;

    public Message(Link<ISource> source, Link<ITarget> target)
    {
        SourceLink = source;
        TargetLink = target;
        Rollback = source.Value.Listener;
    }

    public Message(ISource source, ITarget target)
        : this(new Link<ISource>(source), new Link<ITarget>(target))
    {
        if (source.Listener?.SourceLink is Link<ISource> nextSource)
        {
            _ = nextSource.SpliceBefore();
            nextSource.Prepend(SourceLink);
        }

        if (source.Listener?.TargetLink is Link<ITarget> nextTarget
            && nextTarget.Value.Status.HasFlag(Status.Tracking))
        {
            _ = nextTarget.SpliceBefore();
            nextTarget.Prepend(TargetLink);
        }
    }

    public Link<ISource> SourceLink { get; }

    public Link<ITarget> TargetLink { get; }

    public bool IsUnused => _version == Unused;

    public Message? Rollback { get; private set; }

    public IEnumerable<ITarget> Targets
    {
        get
        {
            for (var target = TargetLink; target is not null; target = target.Next)
            {
                yield return target.Value;
            }
        }
    }

    public IEnumerable<ISource> Sources
    {
        get
        {
            for (var source = SourceLink; source is not null; source = source.Next)
            {
                yield return source.Value;
            }
        }
    }

    public bool ShouldRefresh
    {
        get
        {
            var source = SourceLink.Value;

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
        if (SourceLink.Value.Listener is Message rollback)
        {
            Rollback = rollback;
        }

        SourceLink.Value.Listener = this; // TODO
        _version = Unused;
    }

    public void Refresh()
    {
        SourceLink.Value.Track(this);
        TargetLink.Value.Watch(this);
        _version = 0;
    }

    public void SyncVersion() => _version = SourceLink.Value.Version;
}
