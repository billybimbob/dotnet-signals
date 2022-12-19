using Signals.Infrastructure.Disposables;

namespace Signals.Infrastructure;

internal sealed class Signal<T> : ISignalSource<T>, ISource, IDisposable
    where T : IEquatable<T>
{
    private readonly Messenger _messenger;
    private readonly HashSet<IObserver<T>> _observers;
    private IDisposable? _subscription;

    private T _value;
    private int _version;

    private Message? _listener;
    private Message? _tracking;

    internal Signal(Messenger messenger, T value)
    {
        _messenger = messenger;
        _observers = new HashSet<IObserver<T>>();
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
        _ = _observers.Add(observer);

        if (_subscription is null)
        {
            _subscription = _messenger.Subscribe(this, _observers);
        }
        else
        {
            observer.OnNext(_value);
        }

        return new SignalCleanup<T>(Cleanup, observer);
    }

    private void Cleanup(IObserver<T> observer)
    {
        _ = _observers.Remove(observer);

        if (_observers.Count is 0)
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    void IDisposable.Dispose()
    {
        _observers.Clear();

        _subscription?.Dispose();
        _subscription = null;

        _listener = null;
        _tracking = null;
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
        var target = message.TargetLink;

        if (_tracking == message)
        {
            _tracking = target.Next?.Value.Watching;
        }

        _ = target.Pop();
    }

    #endregion
}
