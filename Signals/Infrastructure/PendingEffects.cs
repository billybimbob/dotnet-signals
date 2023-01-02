namespace Signals.Infrastructure;

internal readonly ref struct PendingEffects
{
    private readonly Action? _effects;

    public bool IsDeferred { get; }

    public PendingEffects(Action effects, bool isDeferred)
    {
        _effects = effects;
        IsDeferred = isDeferred;
    }

    public void Finish() => _effects?.Invoke();
}
