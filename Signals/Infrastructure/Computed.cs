using Signals.Infrastructure.Subscription;

namespace Signals.Infrastructure;

internal sealed class Computed<T> : ISignal<T>, ISource, ITarget, ISubscriber<T>
    where T : IEquatable<T>
{
    private readonly Messenger _messenger;
    private int _lastMessage;

    private SubscribeEffect<T>? _subscription;

    private readonly Func<T> _compute;
    private Exception? _exception;
    private T _value;

    private int _version;
    private Status _status;

    private Message? _watching;
    private Message? _listener;
    private Message? _tracking;

    internal Computed(Messenger messenger, Func<T> compute)
    {
        _messenger = messenger;
        _lastMessage = messenger.Version - 1;

        _compute = compute;
        _value = default!; // _value is computed lazily with Recompute

        _version = 0;
        _status = Status.Outdated;
    }

    T ISignal<T>.Peek
    {
        get
        {
            if (_status.HasFlag(Status.Running))
            {
                throw new InvalidOperationException("Cycle detected");
            }

            Refresh();

            if (_exception is not null)
            {
                throw _exception;
            }

            return _value;
        }
    }

    public T Value
    {
        get
        {
            if (_status.HasFlag(Status.Running))
            {
                throw new InvalidOperationException("Cycle detected");
            }

            var dependency = _messenger.AddDependency(this);

            Refresh();
            dependency?.SyncVersion();

            if (_exception is not null)
            {
                throw _exception;
            }

            return _value;
        }
    }

    private void Refresh()
    {
        _status &= ~Status.Notified;

        if (_status.HasFlag(Status.Tracking) && !_status.HasFlag(Status.Outdated))
        {
            return;
        }

        _status &= ~Status.Outdated;

        if (_lastMessage == _messenger.Version)
        {
            return;
        }

        _lastMessage = _messenger.Version;
        _status |= Status.Running;

        Recompute();

        _status &= ~Status.Running;
    }

    private void Recompute()
    {
        if (_version != 0 && !Lifecycle.Refresh(_watching))
        {
            return;
        }

        Lifecycle.Backup(ref _watching);

        var watcher = _messenger.Watcher;
        _messenger.Watcher = this;

        try
        {
            var value = _compute.Invoke();

            if (!value.Equals(_value))
            {
                _exception = null;
                _value = value;
                _version++;
            }
        }
        catch (Exception e)
        {
            _exception = e;
            _value = default!;
            _version++;
        }
        finally
        {
            Lifecycle.Prune(ref _watching);
            _messenger.Watcher = watcher;
        }
    }

    public IDisposable Subscribe(IObserver<T> observer)
    {
        if (_status.HasFlag(Status.Running))
        {
            throw new InvalidOperationException("Cycle Detected");
        }

        _subscription ??= _messenger.Subscribe(this);
        _subscription.Add(observer);

        return new SubscriptionCleanup<T>(this, observer);
    }

    SubscribeEffect<T>? ISubscriber<T>.Target
    {
        get => _subscription;
        set => _subscription = value;
    }

    int ISource.Version => _version;

    Message? ISource.Listener
    {
        get => _listener;
        set => _listener = value;
    }

    bool ISource.Refresh()
    {
        _status &= ~Status.Notified;

        if (_status.HasFlag(Status.Running))
        {
            return false;
        }

        Refresh();

        return true;
    }

    void ISource.Track(Message message)
    {
        if (message != _listener)
        {
            return;
        }

        if (message == _tracking)
        {
            return;
        }

        var target = message.TargetLink;

        if (!target.Value.Status.HasFlag(Status.Tracking))
        {
            return;
        }

        if (_tracking is null)
        {
            TrackWatcher(message);
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

    private void TrackWatcher(Message message)
    {
        _status |= Status.Outdated | Status.Tracking;

        if (_watching is null)
        {
            return;
        }

        foreach (var source in _watching.Sources)
        {
            source.Track(message);
        }
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

        if (_tracking is not null)
        {
            return;
        }

        if (_watching is null)
        {
            return;
        }

        foreach (var source in _watching.Sources)
        {
            source.Untrack(message);
        }
    }

    Status ITarget.Status => _status;

    Message? ITarget.Watching
    {
        get => _watching;
        set
        {
            if (value == _watching)
            {
                return;
            }

            if (value is { IsUnused: false })
            {
                return;
            }

            if (value is { TargetLink.IsFirst: true })
            {
                return;
            }

            if (_watching is { TargetLink: var oldTarget }
                && value is { TargetLink: var target }
                && oldTarget != target)
            {
                _ = oldTarget.SpliceBefore();
                oldTarget.Prepend(target.Pop());
            }

            _watching = value;
        }
    }

    void ITarget.Notify()
    {
        if (_status.HasFlag(Status.Notified))
        {
            return;
        }

        _status |= Status.Outdated | Status.Notified;

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
