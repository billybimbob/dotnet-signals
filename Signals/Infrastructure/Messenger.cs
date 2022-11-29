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
    private LinkedList<ISource>? _versions;
    private LinkedList<ITarget>? _subscriptions;

    private LinkedList<ISource> Versions => _versions ??= new();

    private LinkedList<ITarget> Subscriptions => _subscriptions ??= new();

    public int Version { get; private set; }

    public IEffect? Effect { get; set; }

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

            Exception? exception = null;

            while (Effect != null)
            {
                var effect = Effect;
                Effect = null;

                _iteration++;

                try
                {
                    while (effect != null)
                    {
                        effect = effect.Run(this);
                    }
                }
                catch (Exception e)
                {
                    effect.Dispose();

                    // TODO: catch multiple exceptions

                    if (exception is null)
                    {
                        exception = e;
                    }
                }
            }

            _iteration = 0;

            if (exception is not null)
            {
                throw exception;
            }
        }
    }

    public Message? AddDependency(ISource source)
    {
        if (_watcher == null)
        {
            return null;
        }

        if (source.Listener is not Message message
            || message.TargetNode.Value != _watcher)
        {
            message = CreateMessage(source, _watcher);
        }

        message.RenewDependency();

        return message;
    }

    private Message CreateMessage(ISource source, ITarget target)
    {
        var sourceSpot = Versions.AddFirst(source);

        bool shouldTrack = target.Status.HasFlag(Status.Tracking);

        var targetSpot = source.Listener?.TargetNode switch
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
}
