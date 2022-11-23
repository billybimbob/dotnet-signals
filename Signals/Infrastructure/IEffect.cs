namespace Signals.Infrastructure;

internal interface IEffect : ITarget
{
    IEffect? Next { get; }

    void Run();

    void Cleanup();
}
