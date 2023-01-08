namespace Signals.Infrastructure;

internal sealed class DisposingEffect : IEffect
{
    private readonly Messenger _messenger;
    private readonly Func<Action> _callback;
    private Action? _cleanup;

    private Status _status;
    private Message? _watching;
    private IEffect? _next;

    public DisposingEffect(Messenger messenger, Func<Action> callback)
    {
        _messenger = messenger;
        _callback = callback;
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

        if (!Lifecycle.Refresh(_watching))
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
        Lifecycle.Backup(ref _watching);

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

            _status &= ~Status.Running;

            if (_status.HasFlag(Status.Disposed))
            {
                Dispose();
            }

            _messenger.Watcher = watcher;
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

        if (_watching is not null)
        {
            foreach (var source in _watching.Sources)
            {
                if (source.Listener is Message listener)
                {
                    source.Untrack(listener);
                }
            }

            _watching = null;
        }

        _next = null;

        // keep eye on, can throw here
        ApplyCleanup();
    }
}
