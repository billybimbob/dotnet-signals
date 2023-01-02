namespace Signals.Infrastructure;

internal sealed class Message
{
    private const int Unused = -1;

    private Message? _rollback;
    private int _version;

    public Message(Link<ISource> source, Link<ITarget> target)
    {
        _rollback = source.Value.Listener;
        _version = 0;
        SourceLink = source;
        TargetLink = target;
    }

    public Message(ISource source, ITarget target)
        : this(
            new Link<ISource>(source),
            new Link<ITarget>(target))
    {
        if (source is { Listener.SourceLink: var nextSource })
        {
            var currentSource = SourceLink.Pop();
            _ = nextSource.SpliceBefore();

            nextSource.Prepend(currentSource);
        }

        if (source is { Listener.TargetLink: var nextTarget }
            && nextTarget.Value.Status.HasFlag(Status.Tracking))
        {
            var currentTarget = TargetLink.Pop();
            _ = nextTarget.SpliceBefore();

            nextTarget.Prepend(currentTarget);
        }
    }

    public Link<ISource> SourceLink { get; }

    public Link<ITarget> TargetLink { get; }

    public bool IsUnused => _version == Unused;

    public IEnumerable<ITarget> Targets
    {
        get
        {
            for (
                var target = TargetLink;
                target is not null;
                target = target.Next)
            {
                yield return target.Value;
            }
        }
    }

    public IEnumerable<ISource> Sources
    {
        get
        {
            for (
                var source = SourceLink;
                source is not null;
                source = source.Next)
            {
                yield return source.Value;
            }
        }
    }

    public bool Refresh()
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

    public void Backup()
    {
        if (SourceLink.Value.Listener is Message rollback)
        {
            _rollback = rollback;
        }

        SourceLink.Value.Listener = this;
        _version = Unused;
    }

    public void Restore()
    {
        SourceLink.Value.Listener = _rollback;

        if (_rollback is not null)
        {
            _rollback = null;
        }
    }

    public void Reset()
    {
        SourceLink.Value.Track(this);
        TargetLink.Value.Watching = this;
        _version = 0;
    }

    public void SyncVersion() => _version = SourceLink.Value.Version;
}
