namespace Signals.Infrastructure;

internal interface ISubscriber<T> where T : IEquatable<T>
{
    SubscribeEffect<T>? Target { get; set; }
}

