namescape Signals.Infrastructure;

internal interface ISubscriber : IDisposable
{
    SubscriberEffect? Target { get; set; }
}

