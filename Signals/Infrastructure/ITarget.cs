namespace Signals.Infrastructure;

internal interface ITarget
{
    Status Status { get; }

    Message? Watching { get; }

    void Watch(Message message);

    void Notify(Messenger messenger);
}
