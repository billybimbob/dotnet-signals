using Signals.Infrastructure;

namespace Signals;

public sealed class SignalProvider
{
    private readonly Messenger _messenger;

    public SignalProvider()
    {
        _messenger = new Messenger();
    }

    public ISignalSource<T> CreateSource<T>(T value) where T : IEquatable<T>
        => new Signal<T>(_messenger, value);

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
