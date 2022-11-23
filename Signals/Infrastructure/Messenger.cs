namespace Signals.Infrastructure;

internal readonly ref struct Pending
{
    private readonly Action? _changes;

    public Pending(Action? changes)
    {
        _changes = changes;
    }

    public void Dispose() => _changes?.Invoke();
}

internal class Messenger
{
    private int _batchDepth;
    private int _iteration;

    private ITarget? _watcher;
    private IEffect? _effect;

    private LinkedList<ISource>? _versions;
    private LinkedList<ITarget>? _subscriptions;

    private LinkedList<ISource> Versions => _versions ??= new();
    private LinkedList<ITarget> Subscriptions => _subscriptions ??= new();

    public int Version { get; private set; }

    public void UpdateVersion() => Version++;

    public Pending StartBatch()
    {
        _batchDepth++;

        return new Pending(FlushEffects);

        void FlushEffects()
        {
            if (_iteration > 100)
            {
                throw new InvalidOperationException("Cycle detected");
            }

            if (_batchDepth > 1)
            {
                _batchDepth--;
                return;
            }
        }
    }

    public static bool RequiresRefresh(ITarget target)
    {
        if (target.Watching is null)
        {
            return false;
        }

        foreach (var source in target.Watching.GetSources())
        {
            if (source.Version != source.Current?.Version)
            {
                return true;
            }

            if (!source.Refresh())
            {
                return true;
            }

            if (source.Version != source.Current?.Version)
            {
                return true;
            }
        }

        return false;
    }

    private static void ClearChanges(ITarget target)
    {
        // foreach (var node in target.Watching.GetTargets())
        // {
        //     node.Rollback = node.Source.Listener.Rollback;

        //     node.Source.Listener = node;

        //     node.Version = Message.Unused;
        // }
    }

    public Message? AddDependency(ISource source)
    {
        if (_watcher == null)
        {
            return null;
        }

        if (source.Current is not Message message
            || message.Target.Value != _watcher)
        {
            message = CreateMessage(source, _watcher);
        }

        message.RenewDependency();

        return message;
    }

    private Message CreateMessage(ISource source, ITarget target)
    {
        bool shouldTrack = target.Status.HasFlag(Status.Tracking);

        var sourceSpot = Versions.AddFirst(source);

        var targetSpot = source.Current?.Target switch
        {
            LinkedListNode<ITarget> sourceTarget when shouldTrack =>
                Subscriptions.AddBefore(sourceTarget, target),

            _ => new LinkedListNode<ITarget>(target)
        };

        return new Message(sourceSpot, targetSpot);
    }

    public Pending Exchange(ITarget newWatcher)
    {
        var oldWatcher = _watcher;
        _watcher = newWatcher;

        return new Pending(Revert);

        void Revert() => _watcher = oldWatcher;
    }

    public Pending Exchange(IEffect newEffect)
    {
        var oldEffect = _effect;
        _effect = newEffect;

        return new Pending(Revert);

        void Revert() => _effect = oldEffect;
    }
}
