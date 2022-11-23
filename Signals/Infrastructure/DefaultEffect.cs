namespace Signals.Infrastructure;

internal sealed class DefaultEffect : ITarget, IEffect
{
    private readonly Messenger _messenger;
    private readonly Action _compute;

    public IEffect? Next { get; private set; }

    Status ITarget.Status => _status;

    Message? ITarget.Watching => _watching;

    private Message? _watching;
    private Status _status;

    public DefaultEffect(Messenger messenger, Action compute)
    {
        _messenger = messenger;
        _compute = compute;
    }

    void IEffect.Run()
    {
        void Stop()
        {
        }
    }

    void IEffect.Cleanup()
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

        foreach (var source in _watching.GetSources())
        {
            if (source.Current is Message message)
            {
                source.Untrack(message);
            }
        }
    }
}
