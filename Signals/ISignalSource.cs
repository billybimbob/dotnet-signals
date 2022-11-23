namespace Signals;

public interface ISignalSource<T> : ISignal<T> where T : IEquatable<T>
{
    new T Value { get; set; }
}
