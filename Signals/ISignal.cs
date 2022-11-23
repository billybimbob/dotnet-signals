namespace Signals;

public interface ISignal<T> where T : IEquatable<T>
{
    T Value { get; }
    T Peek { get; }

    void Subscribe(Action<T> subscriber);
}
