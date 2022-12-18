using Signals.Infrastructure.Disposables;

namespace Signals.Infrastructure;

internal sealed class Computed<T> : ISignal<T>, ISource, ITarget
    where T : IEquatable<T>
{
    private readonly Messenger _messenger;
    private int _lastMessage;

    private int _version;
    private Status _status;

    private readonly HashSet<IObserver<T>> _observers;
    private IDisposable? _subscription;

    private Message? _watching;
    private Message? _listener;
    private Message? _tracking;

    private readonly Func<T> _compute;
    private Exception? _exception;
    private T _value;

    internal Computed(Messenger messenger, Func<T> compute)
    {
        _messenger = messenger;
        _lastMessage = messenger.Version - 1;

        _version = 0;
        _status = Status.Outdated;

        _observers = new HashSet<IObserver<T>>();

        _compute = compute;
        _value = default!; // _value is computed lazily with Recompute
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

        Backup();
        Recompute();
        Prune();

        _status &= ~Status.Running;
    }

    private void Backup()
    {
        if (_watching is null)
        {
            return;
        }

        foreach (var source in _watching.Sources)
        {
            source.Listener?.Backup();
        }
    }

    private void Recompute()
    {
        if (!RefreshWatching())
        {
            return;
        }

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
            _messenger.Watcher = watcher;
        }
    }

    private bool RefreshWatching()
    {
        if (_version == 0)
        {
            return true;
        }

        if (_watching is null)
        {
            return true;
        }

        foreach (var source in _watching.Sources)
        {
            if (source.Listener?.Refresh() is true)
            {
                return true;
            }
        }

        return false;
    }

    private void Prune()
    {
        Message? watching = null;

        var source = _watching?.SourceLink;

        // use while loop since source is modified during iter

        while (source is not null)
        {
            var next = source.Next;
            watching = source.Cleanup(watching);
            source = next;
        }

        _watching = watching;
    }

    public IDisposable Subscribe(IObserver<T> observer)
    {
        if (_status.HasFlag(Status.Running))
        {
            throw new InvalidOperationException("Cycle Detected");
        }

        _ = _observers.Add(observer);

        if (_subscription is null)
        {
            _subscription = _messenger.Subscribe(this, _observers);
        }
        else
        {
            NotifyCurrent(observer);
        }

        return new SignalCleanup<T>(Cleanup, observer);
    }

    private void NotifyCurrent(IObserver<T> observer)
    {
        Refresh();

        if (_exception is not null)
        {
            observer.OnError(_exception);
        }
        else
        {
            observer.OnNext(_value);
        }
    }

    private void Cleanup(IObserver<T> observer)
    {
        _ = _observers.Remove(observer);

        if (_observers.Count is 0)
        {
            _subscription?.Dispose();
        }
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

        if (_tracking is null)
        {
            TrackListener(message);
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

    private void TrackListener(Message message)
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
