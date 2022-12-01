namespace Signals.Infrastructure;

internal sealed class DefaultEffect : IEffect
{
    private readonly Messenger _messenger;
    private readonly Action _callback;

    private Status _status;
    private Message? _watching;
    private IEffect? _next;

    public DefaultEffect(Messenger messenger, Action callback)
    {
        _status = Status.Tracking;
        _messenger = messenger;
        _callback = callback;
    }

    Status ITarget.Status => _status;

    Message? ITarget.Watching => _watching;

    void ITarget.Watch(Message message)
    {
        if (message == _watching)
        {
            return;
        }

        if (!message.IsUnused)
        {
            return;
        }

        if (message.TargetLink.IsFirst)
        {
            return;
        }

        if (message.TargetLink is var target
            && _watching?.TargetLink is Link<ITarget> oldTarget
            && target != oldTarget)
        {
            target.Pop();
            _ = oldTarget.SpliceBefore();

            oldTarget.Prepend(target);
        }

        _watching = message;
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

        bool? hasChanges = _watching
            ?.Sources.Any(s => s.Listener?.ShouldRefresh ?? false);

        if (hasChanges is false)
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

        _status &= ~Status.Disposed;

        Backup();

        using var effects = _messenger.ApplyEffects();
        using var swap = _messenger.Exchange(this);

        _status |= Status.Running;

        try
        {
            _callback.Invoke();
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

        source.Pop();
        listener.Restore();

        if (listener.IsUnused)
        {
            source.Value.Untrack(listener);
        }
        else
        {
            if (root is { SourceLink: var rootSource })
            {
                rootSource.Prepend(source);
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
