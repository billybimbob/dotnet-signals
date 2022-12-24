namespace Signals.Infrastructure;

internal sealed class SubscribeEffect<T> : IEffect where T : IEquatable<T>
{
    private readonly Messenger _messenger;
    private readonly ISignal<T> _source;
    private readonly HashSet<IObserver<T>> _observers;

    private T _value;
    private Exception? _exception;

    private Status _status;
    private Message? _watching;
    private IEffect? _next;

    public SubscribeEffect(Messenger messenger, ISignal<T> source)
    {
        _messenger = messenger;
        _source = source;
        _observers = new HashSet<IObserver<T>>();

        _value = default!;
        _status = Status.Outdated | Status.Tracking;
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
            _value = value;
            _exception = null;

            foreach (var observer in _observers)
            {
                // keep eye on exceptions that occur here
                // thrown exceptions can potentially lead
                // to partial observation updates

                observer.OnNext(value);
            }
        }
        catch (Exception e)
        {
            _status &= ~Status.Tracking;
            _value = default!;
            _exception = e;

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
            _status &= ~Status.Outdated;
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

    public void Add(IObserver<T> observer)
    {
        bool isNew = _observers.Add(observer);

        if (!isNew)
        {
            return;
        }

        if (_status.HasFlag(Status.Outdated))
        {
            return;
        }

        if (_exception is not null)
        {
            observer.OnError(_exception);
            throw _exception;
        }
        else
        {
            observer.OnNext(_value);
        }
    }

    /// <returns> true the effect has no more observers </returns>
    public bool Remove(IObserver<T> observer)
    {
        _ = _observers.Remove(observer);

        return _observers.Count is 0;
    }

    public void Dispose()
    {
        _status |= Status.Disposed;

        if (!_status.HasFlag(Status.Running))
        {
            return;
        }

        foreach (var observer in _observers)
        {
            observer.OnCompleted();
        }
        
        _observers.Clear();
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
