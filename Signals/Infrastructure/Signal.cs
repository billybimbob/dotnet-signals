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

            using var effects = _messenger.ApplyEffects();

            if (_tracking is null)
            {
                return;
            }

            foreach (var target in _tracking.Targets)
            {
                target.Notify();
            }
        }
    }

    Message? ISource.Listener => _listener;

    bool ISource.Refresh() => true;

    void ISource.Track(Message message)
    {
        if (_listener is not null && _listener.TargetLink == message.TargetLink)
        {
            return;
        }

        _listener = message;

        var target = message.TargetLink;

        if (!target.Value.Status.HasFlag(Status.Tracking))
        {
            return;
        }

        if (message == _tracking)
        {
            return;
        }

        if (!target.IsFirst)
        {
            return;
        }

        if (_tracking?.TargetLink is Link<ITarget> oldTarget
            && oldTarget != target)
        {
            _ = target.SpliceAfter();
            oldTarget.Prepend(target);
        }

        _tracking = message;
    }

    void ISource.Untrack(Message message)
    {
        var target = message.TargetLink;

        if (_tracking == message)
        {
            _tracking = target.Next?.Value.Watching;
        }

        target.Pop();
    }
}
