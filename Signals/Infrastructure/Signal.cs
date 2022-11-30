namespace Signals.Infrastructure;

internal sealed class Signal<T> : ISignalSource<T>, ISource
    where T : IEquatable<T>
{
    private readonly Messenger _messenger;
    private Message? _listener;
    private Message? _tracking;

    internal Signal(Messenger messenger, T value)
    {
        _messenger = messenger;
        Version = 0;
        Peek = value;
    }

    public int Version { get; private set; }

    public T Peek { get; private set; }

    public T Value
    {
        get
        {
            var dependency = _messenger.AddDependency(this);
            dependency?.SyncVersion();

            return Peek;
        }
        set
        {
            if (Peek.Equals(value))
            {
                return;
            }

            Peek = value;
            Version++;

            _messenger.UpdateVersion();

            using var batch = _messenger.ApplyEffects();

            if (_tracking is null)
            {
                return;
            }

            foreach (var target in _tracking.Targets)
            {
                target.Notify(_messenger);
            }
        }
    }

    Message? ISource.Listener => _listener;

    bool ISource.Refresh() => true;

    void ISource.Track(Message message)
    {
        if (_listener is null || _listener != message)
        {
            ListenTo(message);
        }
        else if (message.IsUnused)
        {
            Reuse(message);
        }
    }

    private void ListenTo(Message message)
    {
        _listener = message;

        var target = message.TargetNode;

        if (!target.Value.Status.HasFlag(Status.Tracking))
        {
            return;
        }

        if (_tracking == message)
        {
            return;
        }

        if (target.Previous is not null)
        {
            return;
        }

        if (_tracking?.TargetNode is LinkedListNode<ITarget> oldTarget)
        {
            oldTarget.List?.Remove(oldTarget);
            target.List?.AddAfter(target, oldTarget);
        }

        _tracking = message;
    }

    private static void Reuse(Message message)
    {
        var source = message.SourceNode;

        if (source.List is not LinkedList<ISource> sourceList)
        {
            return;
        }

        if (sourceList.First != message.SourceNode)
        {
            sourceList.Remove(source);
            sourceList.AddFirst(source);
        }
    }

    void ISource.Untrack(Message message)
    {
        var target = message.TargetNode;

        if (_tracking == message)
        {
            _tracking = target.Next?.Value.Watching;
        }

        target.List?.Remove(target);
    }
}
