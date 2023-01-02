namespace Signals.Infrastructure.Subscription;

internal interface ISubscriber<T> where T : IEquatable<T>
{
    SubscribeEffect<T>? Target { get; set; }
}

