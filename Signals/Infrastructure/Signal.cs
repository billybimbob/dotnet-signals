namespace Signals.Infrastructure;

internal sealed class Signal<T> : ISignalSource<T>, ISource
    where T : IEquatable<T>
{
    private readonly Messenger _messenger;
    private Message? _current;
    private Message? _listener;

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
            _messenger
                .AddDependency(this)
                ?.UpdateVersion();

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

            using var batch = _messenger.StartBatch();

            if (_listener is null)
            {
                return;
            }

            foreach (var target in _listener.GetTargets())
            {
                target.Notify();
            }
        }
    }

    // public void Subscribe(Action<T> subscriber)
    // {
    // }

    Message? ISource.Current => _current;

    Message? ISource.Listener => _listener;

    bool ISource.Refresh() => true;

    void ISource.Track(Message message)
    {
        if (_current == null || _current != message)
        {
            ListenTo(message);
        }
        else if (message.Version == Message.Unused)
        {
            Reuse(message);
        }
    }

    private static void Reuse(Message message)
    {
        if (message.Source.List is not LinkedList<ISource> sources)
        {
            return;
        }

        if (sources.First != message.Source)
        {
            sources.Remove(message.Source);
            sources.AddFirst(message.Source);
        }
    }

    private void ListenTo(Message message)
    {
        _current = message;

        var target = message.Target;

        if (!target.Value.Status.HasFlag(Status.Tracking))
        {
            return;
        }

        if (_listener == message)
        {
            return;
        }

        if (target.Previous is not null)
        {
            return;
        }

        if (_listener?.Target is LinkedListNode<ITarget> oldTarget)
        {
            oldTarget.List?.Remove(oldTarget);
            target.List?.AddAfter(target, oldTarget);
        }

        _listener = message;
    }

    void ISource.Untrack(Message message)
    {
        var target = message.Target;

        if (_listener == message)
        {
            _listener = target.Next?.Value.Watching;
        }

        target.List?.Remove(target);
    }
}
