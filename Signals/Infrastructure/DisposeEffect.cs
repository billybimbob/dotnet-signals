namespace Signals.Infrastructure;

internal sealed class DisposingEffect : IEffect
{
    private readonly Messenger _messenger;
    private readonly Func<Action> _callback;
    private Action? _cleanup;

    private Status _status;
    private Link<Message>? _watching;
    private IEffect? _next;

    public DisposingEffect(Messenger messenger, Func<Action> callback)
    {
        _messenger = messenger;
        _callback = callback;
        _status = Status.Tracking;
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
                _ = _watching.SpliceAfter();
                var last = value.Pop();

                _watching.Append(last);
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

        ApplyCleanup();
        Lifecycle.Reset(ref _watching);

        var watcher = _messenger.Watcher;
        var effects = _messenger.StartEffects();

        _messenger.Watcher = this;

        try
        {
            _cleanup = _callback.Invoke();
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

    private void ApplyCleanup()
    {
        if (_cleanup is not Action cleanup)
        {
            return;
        }

        _cleanup = null;

        var watcher = _messenger.Watcher;
        var effects = _messenger.StartEffects();

        _messenger.Watcher = null;

        try
        {
            cleanup.Invoke();
        }
        catch (Exception)
        {
            _status &= ~Status.Running;
            Dispose();
            throw;
        }
        finally
        {
            _messenger.Watcher = watcher;
            effects.Finish();
        }
    }

    public void Dispose()
    {
        _status |= Status.Disposed;

        if (_status.HasFlag(Status.Running))
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

        _watching = null;
        _next = null;

        // keep eye on, can throw here
        ApplyCleanup();
    }
}
