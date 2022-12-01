namespace Signals.Infrastructure;

internal sealed class SignalCleanup<T> : IDisposable
{
    private readonly ICollection<IObserver<T>> _observers;
    private readonly IObserver<T> _observer;

    public SignalCleanup(ICollection<IObserver<T>> observers, IObserver<T> observer)
    {
        _observers = observers;
        _observer = observer;
    }

    void IDisposable.Dispose() => _observers.Remove(_observer);
}
