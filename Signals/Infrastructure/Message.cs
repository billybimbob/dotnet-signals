namespace Signals.Infrastructure;

internal sealed class Message
{
    public const int Unused = -1;

    public LinkedListNode<ISource> Source { get; }

    public LinkedListNode<ITarget> Target { get; }

    public int Version { get; private set; }

    public Message? Rollback { get; private set; }

    public Message(LinkedListNode<ISource> source, LinkedListNode<ITarget> target)
    {
        Source = source;
        Target = target;
        Rollback = source.Value.Current;
    }

    public bool ShouldRenew(ITarget target) =>
        Target.Value == target && Version == Unused;

    public IEnumerable<ITarget> GetTargets()
    {
        for (var target = Target; target != null; target = target.Next)
        {
            yield return target.Value;
        }
    }

    public IEnumerable<ISource> GetSources()
    {
        for (var source = Source; source != null; source = source.Next)
        {
            yield return source.Value;
        }
    }

    public void RenewDependency()
    {
        Source.Value.Track(this);
        Target.Value.Watch(this);
        Version = 0;

        // if (Source.Value == Source.List?.First?.Value)
        // {
        //     return;
        // }

        // Source.List?.Remove(Source);
        // sourceList.AddFirst(sourceSpot);

        // sourceList.Remove(targetSpot);
        // sourceList.AddAfter(sourceSpot, targetSpot);
    }

    public void UpdateVersion() => Version = Source.Value.Version;
}
