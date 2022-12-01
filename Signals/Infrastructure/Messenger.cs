namespace Signals.Infrastructure;

internal sealed class Messenger
{
    private int _batchDepth;
    private int _iteration;
    private ITarget? _watcher;

    public int Version { get; private set; }

    public IEffect? Effect { get; set; }

    public void UpdateVersion() => Version++;

    public Pending ApplyEffects()
    {
        _batchDepth++;

        return new Pending(FlushEffects);

        void FlushEffects()
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
    }

    public Message? AddDependency(ISource source)
    {
        if (_watcher == null)
        {
            return null;
        }

        if (source.Listener is not Message dependency
            || dependency.TargetLink.Value != _watcher)
        {
            dependency = new Message(source, _watcher);
        }

        dependency.Reset();

        return dependency;
    }

    public Pending Exchange(ITarget newWatcher)
    {
        var oldWatcher = _watcher;
        _watcher = newWatcher;

        return new Pending(Revert);

        void Revert() => _watcher = oldWatcher;
    }
}
