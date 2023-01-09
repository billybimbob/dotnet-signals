using Signals.Infrastructure.Subscription;

namespace Signals.Infrastructure;

internal sealed class Signal<T> : ISignalSource<T>, ISource, ISubscriber<T>
    where T : IEquatable<T>
{
    private readonly Messenger _messenger;
    private SubscribeEffect<T>? _subscription;

    private T _value;
    private int _version;

    private Link<Message>? _listener;
    private Link<Message>? _tracking;

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
            if (_messenger.Watcher is not null and not IEffect)
            {
                throw new InvalidOperationException("Mutation from illegal source");
            }

            if (_value.Equals(value))
            {
                return;
            }

            var effects = _messenger.StartEffects();

            _value = value;
            _version++;

            _messenger.Notify();

            for (
                var track = _tracking;
                track is not null;
                track = track.Next)
            {
                track.Value.Target.Notify();
            }

            effects.Finish();
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

    Link<Message>? ISource.Listener
    {
        get => _listener;
        set => _listener = value;
    }

    bool ISource.Update() => true;

    void ISource.Track(Link<Message> link)
    {
        if (link != _listener)
        {
            return;
        }

        if (link == _tracking)
        {
            return;
        }

        var target = link.Value.Target;

        if (!target.Status.HasFlag(Status.Tracking))
        {
            return;
        }

        if (!link.IsFirst)
        {
            return;
        }

        if (_tracking is not null)
        {
            var first = link.Pop();
            _tracking.Prepend(first);
        }

        _tracking = link;
    }

    void ISource.Untrack(Link<Message> link)
    {
        if (_tracking is null)
        {
            return;
        }

        if (_tracking == link)
        {
            _tracking = link.Next;
        }

        _ = link.Pop();
    }

    #endregion
}
