using System;

namespace Signals.Infrastructure;

internal sealed class DefaultEffect : ITarget, IEffect
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

    void ITarget.Notify(Messenger messenger)
    {
        if (!_status.HasFlag(Status.Notified))
        {
            _status |= Status.Notified;
            _next = messenger.Effect;
            messenger.Effect = this;
        }
    }

    IEffect IEffect.Run(Messenger messenger)
    {
        if (_status.HasFlag(Status.Running))
        {
            throw new InvalidOperationException("Cycle detected");
        }

        _status |= Status.Running;
        _status &= ~Status.Disposed;

        Backup();

        using var batch = messenger.StartBatch();
        using var swap = messenger.Exchange(this);

        _compute.Invoke();
        _status &= ~Status.Running;

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
            source.Backup();
        }
    }

    void IDisposable.Dispose()
    {
        _status |= Status.Disposed;

        if (!_status.HasFlag(Status.Running))
        {
            return;
        }

        if (_watching is null)
        {
            return;
        }

        foreach (var source in _watching.Sources)
        {
            if (source.Listener is Message message)
            {
                source.Untrack(message);
            }
        }
    }
}
