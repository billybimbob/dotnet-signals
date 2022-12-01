namespace Signals;

public interface ISignal<T> : IObservable<T> where T : IEquatable<T>
{
    T Value { get; }

    T Peek { get; }
}
