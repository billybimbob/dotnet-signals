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
        _status = Status.Tracking;
        _messenger = messenger;
        _callback = callback;
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

        try
        {
            ApplyCleanup();
            Backup();

            using var effects = _messenger.ApplyEffects();
            using var exchange = _messenger.Exchange(this);

            _cleanup = _callback.Invoke();
        }
        finally
        {
            Prune();

            _status &= ~Status.Running;

            if (_status.HasFlag(Status.Disposed))
            {
                Dispose();
            }
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

    private void ApplyCleanup()
    {
        if (_cleanup is not Action cleanup)
        {
            return;
        }

        _cleanup = null;

        using var effects = _messenger.ApplyEffects();
        using var exchange = _messenger.Exchange(this);

        try
        {
            cleanup.Invoke();
        }
        catch (Exception)
        {
            Dispose();
            throw;
        }
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

    private void Prune()
    {
        Message? watching = null;

        var source = _watching?.SourceLink;

        // use while loop since source is modified during iter

        while (source is not null)
        {
            var next = source.Next;
            watching = Cleanup(source, watching);
            source = next;
        }

        _watching = watching;
    }

    private static Message? Cleanup(Link<ISource> source, Message? root)
    {
        if (source.Value.Listener is not Message listener)
        {
            throw new InvalidOperationException("Source is missing listener");
        }

        listener.Restore();

        if (listener.IsUnused)
        {
            _ = source.Pop();
            source.Value.Untrack(listener);
        }
        else
        {
            if (root is { SourceLink: var rootSource })
            {
                rootSource.Prepend(source.Pop());
            }

            root = listener;
        }

        return root;
    }

    public void Dispose()
    {
        _status |= Status.Disposed;

        if (!_status.HasFlag(Status.Running))
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

        ApplyCleanup();
    }
}
