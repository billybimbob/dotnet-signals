namespace Signals.Infrastructure;

internal sealed class SubscribeEffect<T> : IEffect where T : IEquatable<T>
{
    private readonly Messenger _messenger;
    private readonly ISignal<T> _source;
    private readonly IReadOnlyCollection<IObserver<T>> _observers;

    private Status _status;
    private Message? _watching;
    private IEffect? _next;

    public SubscribeEffect(
        Messenger messenger,
        ISignal<T> source,
        IReadOnlyCollection<IObserver<T>> observers)
    {
        _messenger = messenger;
        _source = source;
        _observers = observers;
        _status = Status.Tracking;
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
        if (!_status.HasFlag(Status.Notified))
        {
            _status |= Status.Notified;
            _next = _messenger.Effect;
            _messenger.Effect = this;
        }
    }

    public IEffect? Run()
    {
        _status &= ~Status.Notified;

        if (!Refresh())
        {
            return _next;
        }

        if (_status.HasFlag(Status.Disposed))
        {
            return _next;
        }

        if (_status.HasFlag(Status.Running))
        {
            throw new InvalidOperationException("Cycle detected");
        }

        _status |= Status.Running;
        _status &= ~Status.Disposed;

        Backup();

        using var effects = _messenger.ApplyEffects();

        var watcher = _messenger.Watcher;
        _messenger.Watcher = this;

        try
        {
            Observe();
        }
        finally
        {
            Prune();

            _status &= ~Status.Running;

            if (_status.HasFlag(Status.Disposed))
            {
                Dispose();
            }

            _messenger.Watcher = watcher;
        }

        return _next;
    }

    private bool Refresh()
    {
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

    private void Observe()
    {
        try
        {
            var value = _source.Value;

            _status &= ~Status.Tracking;

            foreach (var observer in _observers)
            {
                observer.OnNext(value);
            }
        }
        catch (Exception e)
        {
            _status &= ~Status.Tracking;

            foreach (var observer in _observers)
            {
                observer.OnError(e);
            }

            Dispose();
            throw;
        }
        finally
        {
            _status |= Status.Tracking;
        }
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

    public void Dispose()
    {
        _status |= Status.Disposed;

        if (!_status.HasFlag(Status.Running))
        {
            return;
        }

        _next = null;

        if (_watching is null)
        {
            return;
        }

        foreach (var source in _watching.Sources)
        {
            if (source.Listener is Message listener)
            {
                source.Untrack(listener);
            }
        }

        _watching = null;
    }
}
