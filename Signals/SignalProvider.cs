using Signals.Infrastructure;

namespace Signals;

public sealed class SignalProvider
{
    private readonly Messenger _messenger;

    public SignalProvider()
    {
        _messenger = new Messenger();
    }

    public ISignalSource<T> Source<T>(T value) where T : IEquatable<T>
        => new Signal<T>(_messenger, value);

    public ISignal<T> Derive<T>(Func<T> compute) where T : IEquatable<T>
        => new Computed<T>(_messenger, compute);

    public IDisposable Watch(Action callback)
    {
        var effect = new DefaultEffect(_messenger, callback);

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

    public IDisposable Watch(Func<Action> callback)
    {
        var effect = new DisposingEffect(_messenger, callback);

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
}
