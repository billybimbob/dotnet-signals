namespace Signals.Infrastructure;

internal sealed class DefaultEffect : IEffect
{
    private readonly Action _compute;

    private Status _status;
    private Message? _watching;
    private IEffect? _next;

    public DefaultEffect(Action compute)
    {
        _compute = compute;
    }

    Status ITarget.Status => _status;

    Message? ITarget.Watching => _watching;

    private IEnumerable<ISource> Sources
        => _watching?.Sources ?? Enumerable.Empty<ISource>();

    private bool HasChanges
        => Sources.Any(s => s.Listener?.ShouldRefresh ?? false);

    void ITarget.Watch(Message message)
    {
        if (_watching == message)
        {
            return;
        }

        if (_watching is not null)
        {
            var oldTarget = _watching.TargetNode;
            var newTarget = message.TargetNode;

            // keep eye on null list

            newTarget.List!.Remove(newTarget);
            oldTarget.List!.AddBefore(oldTarget, newTarget);
        }

        _watching = message;
    }

    void ITarget.Notify(Messenger messenger)
    {
        if (!_status.HasFlag(Status.Notified))
        {
            _status |= Status.Notified;
            _next = messenger.Effect;
            messenger.Effect = this;
        }
    }

    IEffect? IEffect.Run(Messenger messenger)
    {
        _status &= ~Status.Notified;

        if (!HasChanges)
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

        using var batch = messenger.ApplyEffects();
        using var swap = messenger.Exchange(this);

        _compute.Invoke();
        _status &= ~Status.Running;

        Prune();

        return _next;
    }

    private void Backup()
    {
        foreach (var source in Sources)
        {
            source.Listener?.Backup();
        }
    }

    private void Prune()
    {
        Message? watching = null;

        // use while loop since source is modified during iter

        var source = _watching?.SourceNode;

        while (source is not null)
        {
            var next = source.Next;

            if (source.Value.Listener is not Message listener)
            {
                throw new InvalidOperationException("Source is missing listener");
            }

            var sourceList = source.List;
            sourceList?.Remove(source);

            if (listener.IsUnused)
            {
                source.Value.Untrack(listener);
            }
            else
            {
                if (watching is { SourceNode: var root })
                {
                    root.List!.AddBefore(root, source);
                }
                else
                {
                    // keep eye on
                    sourceList?.AddFirst(source);
                }

                watching = listener;
            }

            // TODO: rollback

            source = next;
        }

        _watching = watching;
    }

    void IDisposable.Dispose()
    {
        _status |= Status.Disposed;

        if (!_status.HasFlag(Status.Running))
        {
            return;
        }

        foreach (var source in Sources)
        {
            if (source.Listener is Message listener)
            {
                source.Untrack(listener);
            }
        }
    }
}
