using Signals.Infrastructure.Disposables;

namespace Signals.Infrastructure;

internal sealed class Messenger
{
    private int _batchDepth;
    private int _iteration;

    public int Version { get; private set; }

    public ITarget? Watcher { get; set; }

    public IEffect? Effect { get; set; }

    public void Notify() => Version++;

    public BatchChange ApplyEffects()
    {
        _batchDepth++;

        return new BatchChange(FlushEffects, _batchDepth > 1);
    }

    private void FlushEffects()
    {
        if (_iteration > 100)
        {
            throw new InvalidOperationException("Cycle detected");
        }

        if (_batchDepth > 1)
        {
            _batchDepth--;
            return;
        }

        Exception? exception = null;

        while (Effect is not null)
        {
            var effect = Effect;
            Effect = null;

            _iteration++;

            try
            {
                while (effect is not null)
                {
                    effect = effect.Run();
                }
            }
            catch (Exception e)
            {
                // TODO: catch multiple exceptions

                effect?.Dispose();

                exception ??= e;
            }
        }

        _iteration = 0;
        _batchDepth--;

        if (exception is not null)
        {
            throw exception;
        }
    }

    public IDisposable Subscribe<T>(ISignal<T> source, IReadOnlyCollection<IObserver<T>> observers)
        where T : IEquatable<T>
    {
        var effect = new SubscribeEffect<T>(this, source, observers);

        try
        {
            _ = effect.Run();
            return effect;
        }
        catch (Exception)
        {
            effect.Dispose();
            throw;
        }
    }

    public Message? AddDependency(ISource source)
    {
        if (Watcher is not ITarget watcher)
        {
            return null;
        }

        if (source.Listener is not Message dependency
            || dependency.TargetLink.Value != watcher)
        {
            dependency = new Message(source, watcher);
        }

        dependency.Reset();

        return dependency;
    }
}
