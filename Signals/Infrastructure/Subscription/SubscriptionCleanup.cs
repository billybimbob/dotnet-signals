namespace Signals.Infrastructure.Subscription;

internal sealed class SubscriptionCleanup<T> : IDisposable
    where T : IEquatable<T>
{
    private readonly ISubscriber<T> _subscriber;
    private readonly IObserver<T> _observer;

    public SubscriptionCleanup(ISubscriber<T> subscriber, IObserver<T> observer)
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

        bool isUnused = _subscriber.Target.Remove(_observer);

        if (isUnused)
        {
            _subscriber.Target.Dispose();
            _subscriber.Target = null;
        }
    }
}
