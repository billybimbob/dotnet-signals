namespace Signals.Infrastructure;

internal interface ITarget
{
    Status Status { get; }

    Link<Message>? Watching { get; set; }

    void Notify();
}
