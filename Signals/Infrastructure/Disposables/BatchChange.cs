namespace Signals.Infrastructure.Disposables;

internal readonly ref struct BatchChange
{
    private readonly Action? _changes;

    public bool IsDeferred { get; }

    public BatchChange(Action changes, bool isDeferred)
    {
        _changes = changes;
        IsDeferred = isDeferred;
    }

    public void Dispose() => _changes?.Invoke();
}
