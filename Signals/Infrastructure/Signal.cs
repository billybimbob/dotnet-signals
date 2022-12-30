using Signals.Infrastructure.Disposables;

namespace Signals.Infrastructure;

internal sealed class Signal<T> : ISignalSource<T>, ISource, ISubscriber<T>
    where T : IEquatable<T>
{
    private readonly Messenger _messenger;
    private SubscribeEffect<T>? _subscription;

    private T _value;
    private int _version;

    private Message? _listener;
    private Message? _tracking;

    internal Signal(Messenger messenger, T value)
    {
        _messenger = messenger;
        _value = value;
        _version = 0;
    }

    T ISignal<T>.Peek => _value;

    public T Value
    {
        get
        {
            var dependency = _messenger.AddDependency(this);
            dependency?.SyncVersion();

            return _value;
        }
        set
        {
            if (_value.Equals(value))
            {
                return;
            }

            _value = value;
            _version++;
            _messenger.Notify();

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

    public IDisposable Subscribe(IObserver<T> observer)
    {
        _subscription ??= _messenger.Subscribe(this);
        _subscription.Add(observer);

        return new SubscriptionCleanup<T>(this, observer);
    }

    SubscribeEffect<T>? ISubscriber<T>.Target
    {
        get => _subscription;
        set => _subscription = value;
    }

    #region ISource impl

    int ISource.Version => _version;

    Message? ISource.Listener
    {
        get => _listener;
        set => _listener = value;
    }

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

        if (_tracking is { TargetLink: var oldTarget } && oldTarget != target)
        {
            oldTarget.Prepend(target.Pop());
        }

        _tracking = message;
    }

    void ISource.Untrack(Message message)
    {
        if (_tracking is null)
        {
            return;
        }

        var target = message.TargetLink;

        if (_tracking == message)
        {
            _tracking = target.Next?.Value.Watching;
        }

        _ = target.Pop();
    }

    #endregion
}
