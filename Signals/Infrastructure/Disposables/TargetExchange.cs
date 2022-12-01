namespace Signals.Infrastructure.Disposables;

internal readonly ref struct TargetExchange
{
    private readonly Action? _exchange;

    public ITarget? Swapped { get; }

    public TargetExchange(Action exchange, ITarget? swapped)
    {
        _exchange = exchange;
        Swapped = swapped;
    }

    public void Dispose() => _exchange?.Invoke();
}
