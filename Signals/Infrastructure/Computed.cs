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

    private Link<Message>? _watching;
    private Link<Message>? _listener;
    private Link<Message>? _tracking;

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

            if (dependency is not null)
            {
                dependency.Version = _version;
            }

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

        Lifecycle.Reset(ref _watching);

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

    Link<Message>? ISource.Listener
    {
        get => _listener;
        set => _listener = value;
    }

    bool ISource.Update()
    {
        _status &= ~Status.Notified;

        if (_tracking is null)
        {
            return false;
        }

        if (_status.HasFlag(Status.Running))
        {
            return false;
        }

        if (_version != _tracking.Value.Version)
        {
            return true;
        }

        Refresh();

        return _version != _tracking.Value.Version;
    }

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

        if (_tracking is null)
        {
            TrackWatcher();
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

    private void TrackWatcher()
    {
        _status |= Status.Outdated | Status.Tracking;

        for (
            var watch = _watching;
            watch is not null;
            watch = watch.Next)
        {
            watch.Value.Source.Track(watch);
        }
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

        if (_tracking is not null)
        {
            return;
        }

        for (
            var watch = _watching;
            watch is not null;
            watch = watch.Next)
        {
            watch.Value.Source.Untrack(watch);
        }
    }

    Status ITarget.Status => _status;

    Link<Message>? ITarget.Watching
    {
        get => _watching;
        set
        {
            if (value == _watching)
            {
                return;
            }

            if (value is { Value.Version: not Message.Unused })
            {
                return;
            }

            if (_watching is not null && value is not null)
            {
                var last = value.Pop();
                _watching.Append(last);
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

        for (
            var link = _tracking;
            link is not null;
            link = link.Next)
        {
            link.Value.Target.Notify();
        }
    }
}
