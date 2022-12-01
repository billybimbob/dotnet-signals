namespace Signals.Infrastructure.Disposables;

internal sealed class SignalCleanup<T> : IDisposable
{
    private readonly Action<IObserver<T>> _cleanup;
    private readonly IObserver<T> _observer;

    public SignalCleanup(Action<IObserver<T>> cleanup, IObserver<T> observer)
    {
        _cleanup = cleanup;
        _observer = observer;
    }

    void IDisposable.Dispose() => _cleanup.Invoke(_observer);
}
