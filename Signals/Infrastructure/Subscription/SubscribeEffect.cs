namespace Signals.Infrastructure.Subscription;

internal sealed class SubscribeEffect<T> : IEffect where T : IEquatable<T>
{
    private readonly Messenger _messenger;
    private readonly ISignal<T> _source;
    private readonly HashSet<IObserver<T>> _observers;

    private T _value;
    private Exception? _exception;

    private Status _status;
    private Link<Message>? _watching;
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

    Link<Message>? ITarget.Watching
    {
        get => _watching;
        set
        {
            if (value == _watching)
            {
                return;
            }

            if (value is { Value.IsUnused: false })
            {
                return;
            }

            if (_watching is not null && value is not null)
            {
                var last = value.Pop();
                _watching.Append(last);
            }

            _watching = value;

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

        if (_watching is not null && !Lifecycle.Refresh(_watching))
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

        Lifecycle.Reset(ref _watching);

        var watcher = _messenger.Watcher;
        var effects = _messenger.StartEffects();

        _messenger.Watcher = this;

        try
        {
            Observe();
        }
        finally
        {
            Lifecycle.Prune(ref _watching);

            _messenger.Watcher = watcher;
            _status &= ~Status.Running;

            if (_status.HasFlag(Status.Disposed))
            {
                Dispose();
            }

            effects.Finish();
        }

        return _next;
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

        if (_status.HasFlag(Status.Running))
        {
            return;
        }

        foreach (var observer in _observers)
        {
            observer.OnCompleted();
        }

        _observers.Clear();

        for (
            var watch = _watching;
            watch is not null;
            watch = watch.Next)
        {
            watch.Value.Source.Untrack(watch);
        }

        _watching = null;
        _next = null;
    }
}
