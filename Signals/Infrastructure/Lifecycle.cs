namespace Signals.Infrastructure;

internal static class Lifecycle
{
    public static bool Refresh(Link<Message>? target)
    {
        if (target is null)
        {
            return false;
        }

        for (var link = target; link is not null; link = link.Next)
        {
            var message = link.Value;

            if (message.IsOutdated)
            {
                return true;
            }

            if (!message.Source.Update())
            {
                return true;
            }

            if (message.IsOutdated)
            {
                return true;
            }
        }

        return false;
    }

    public static void Reset(ref Link<Message>? target)
    {
        if (target is null)
        {
            return;
        }

        Link<Message>? last = null;

        for (
            var link = target;
            link is not null;
            link = link.Next)
        {
            var message = link.Value;

            message.Backup();
            message.Source.Listener = link;

            if (link.IsLast)
            {
                last = link;
            }
        }

        if (last is not null)
        {
            target = last;
        }
    }

    public static void Prune(ref Link<Message>? target)
    {
        Link<Message>? first = null;
        var link = target;

        // use while loop since source is modified during iter

        while (link is not null)
        {
            var message = link.Value;
            var previous = link.Previous;

            if (message.IsUnused)
            {
                message.Source.Untrack(link);
                _ = link.Pop();
            }
            else
            {
                first = link;
            }

            message.Restore();

            link = previous;
        }

        target = first;
    }
}
