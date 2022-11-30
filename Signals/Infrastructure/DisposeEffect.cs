namespace Signals.Infrastructure;

internal sealed class DisposingEffect : IEffect
{
    private readonly Func<Action> _compute;
    private Action? _cleanup;

    private IEffect? _next;
    private Message? _watching;
    private Status _status;

    public DisposingEffect(Func<Action> compute)
    {
        _compute = compute;
    }
}
