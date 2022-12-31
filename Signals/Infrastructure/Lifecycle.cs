namespace Signals.Infrastructure;

internal static class Lifecycle
{
    public static bool Refresh(Message? target)
    {
        if (target is null)
        {
            return true;
        }

        foreach (var source in target.Sources)
        {
            if (source.Listener?.Refresh() is true)
            {
                return true;
            }
        }

        return false;
    }

    public static void Backup(ref Message? target)
    {
        if (target is null)
        {
            return;
        }

        Message? tail = null;

        foreach (var source in target.Sources)
        {
            if (source.Listener is not Message listener)
            {
                continue;
            }

            listener.Backup();

            if (listener.SourceLink.IsLast)
            {
                tail = listener;
            }
        }

        if (tail is not null)
        {
            target = tail;
        }
    }

    public static void Prune(ref Message? target)
    {
        Message? head = null;
        var source = target?.SourceLink;

        // use while loop since source is modified during iter

        while (source is not null)
        {
            if (source.Value.Listener is not Message listener)
            {
                throw new InvalidOperationException("Source is missing listener");
            }

            var previous = source.Previous;

            if (listener.IsUnused)
            {
                source.Value.Untrack(listener);
                _ = source.Pop();
            }
            else
            {
                head = listener;
            }

            listener.Restore();
            source = previous;
        }

        target = head;
    }
}