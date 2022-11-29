namespace Signals.Infrastructure;

internal sealed class Message
{
    public const int Unused = -1;

    public LinkedListNode<ISource> SourceNode { get; }

    public LinkedListNode<ITarget> TargetNode { get; }

    public int Version { get; private set; }

    public Message? Rollback { get; private set; }

    public Message(LinkedListNode<ISource> source, LinkedListNode<ITarget> target)
    {
        SourceNode = source;
        TargetNode = target;
        Rollback = source.Value.Listener;
    }

    public IEnumerable<ITarget> Targets
    {
        get
        {
            for (var target = TargetNode; target != null; target = target.Next)
            {
                yield return target.Value;
            }
        }
    }

    public IEnumerable<ISource> Sources
    {
        get
        {
            for (var source = SourceNode; source != null; source = source.Next)
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

            if (Version != source.Listener?.Version)
            {
                return true;
            }

            if (!source.Refresh())
            {
                return true;
            }

            if (Version != source.Listener?.Version)
            {
                return true;
            }
            
            return false;
        }
    }

    public void Backup()
    {
        if (SourceNode.Value.Listener is Message rollback);
        {
            Rollback = rollback;
        }

        SourceNode.Value.Listener = this; // TODO
        Version = Unused;
    }

    public void RenewDependency()
    {
        SourceNode.Value.Track(this);
        TargetNode.Value.Watch(this);
        Version = 0;

        // if (Source.Value == Source.List?.First?.Value)
        // {
        //     return;
        // }

        // Source.List?.Remove(Source);
        // sourceList.AddFirst(sourceSpot);

        // sourceList.Remove(targetSpot);
        // sourceList.AddAfter(sourceSpot, targetSpot);
    }

    public void UpdateVersion() => Version = SourceNode.Value.Version;
}
