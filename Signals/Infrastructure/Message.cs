namespace Signals.Infrastructure;

internal sealed class Message
{
    public const int Unused = -1;

    public Message(ISource source, ITarget target)
    {
        Version = Unused;
        Source = source;
        Target = target;
        Rollback = source.Listener;
    }

    public ISource Source { get; }

    public ITarget Target { get; }

    public int Version { get; set; }

    public Link<Message>? Rollback { get; set; }

    // public IEnumerable<ITarget> Targets
    // {
    //     get
    //     {
    //         for (
    //             var target = TargetLink;
    //             target is not null;
    //             target = target.Next)
    //         {
    //             yield return target.Value;
    //         }
    //     }
    // }

    // public IEnumerable<ISource> Sources
    // {
    //     get
    //     {
    //         for (
    //             var source = SourceLink;
    //             source is not null;
    //             source = source.Next)
    //         {
    //             yield return source.Value;
    //         }
    //     }
    // }

    // public void Backup()
    // {
    //     if (SourceLink.Listener is Link<Message> rollback)
    //     {
    //         Rollback = rollback;
    //     }

    //     SourceLink.Value.Listener = this;
    //     Version = Unused;
    // }

    // public void Restore()
    // {
    //     SourceLink.Value.Listener = Rollback;
    //     Rollback = null;
    // }

    // public void Renew()
    // {
    //     var source = SourceLink.Value;

    //     source.Listener = this;
    //     source.Track(this);

    //     TargetLink.Value.Watching = this;

    //     if (Version == Unused)
    //     {
    //         Version = 0;
    //     }
    // }
}
