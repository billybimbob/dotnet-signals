namespace Signals.Infrastructure;

internal sealed class Computed<T> : ISignal<T>, ISource, ITarget
    where T : IEquatable<T>
{
}
