using Signals.Infrastructure.Subscription;

namespace Signals.Infrastructure;

internal sealed class Messenger
{
    private int _batchDepth;
    private int _iteration;

    public int Version { get; private set; }

    public ITarget? Watcher { get; set; }

    public IEffect? Effect { get; set; }

    public void Notify() => Version++;

    public PendingEffects StartEffects()
    {
        _batchDepth++;

        return new PendingEffects(FlushEffects, _batchDepth > 1);
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

    public SubscribeEffect<T> Subscribe<T>(ISignal<T> source)
        where T : IEquatable<T>
    {
        var effect = new SubscribeEffect<T>(this, source);

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

        var dependency = source.Listener;

        if (dependency?.Value.Target != watcher)
        {
            var message = new Message(source, watcher);
            dependency = new Link<Message>(message);
        }

        source.Listener = dependency;
        source.Track(dependency);

        watcher.Watching = dependency;

        if (Version == Message.Unused)
        {
            Version = 0;
        }

        return dependency.Value;
    }
}
