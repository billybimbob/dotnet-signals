namespace Signals.Infrastructure;

internal readonly ref struct Pending
{
    private readonly Action? _changes;

    public Pending(Action? changes)
    {
        _changes = changes;
    }

    public void Dispose() => _changes?.Invoke();
}
