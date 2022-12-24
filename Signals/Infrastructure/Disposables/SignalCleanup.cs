namespace Signals.Infrastructure.Disposables;

internal sealed class SignalCleanup<T> : IDisposable
{
    private readonly ISubscriber _subscriber;
    private readonly IObserver<T> _observer;

    public SignalCleanup(ISubscriber subscriber, IObserver<T> observer)
    {
        _subscriber = subscriber;
        _observer = observer;
    }

    void IDisposable.Dispose()
    {
        if (_subscriber.Target is null)
        {
            return;
        }

        _subscriber.Target.Remove(_observer);

        if (_subscriber.Target.IsUnused)
        {
            _subscriber.Target.Dispose();
            _subscriber.Target = null;
        }
    }
}
