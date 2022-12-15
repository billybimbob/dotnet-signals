namespace Signals.Infrastructure;

internal sealed class Link<T>
{
    public T Value { get; }

    public Link<T>? Previous { get; private set; }

    public Link<T>? Next { get; private set; }

    public bool IsFirst => Previous is null;

    public bool IsLast => Next is null;

    public Link(T value)
    {
        Value = value;
    }

    public SingleLink Pop()
    {
        if (Previous is not null)
        {
            Previous.Next = Next;
        }

        Previous = null;

        if (Next is not null)
        {
            Next.Previous = Previous;
        }

        Next = null;

        return new SingleLink(this);
    }

    public Link<T>? SpliceBefore()
    {
        if (Previous is not Link<T> tail)
        {
            return null;
        }

        Previous = null;
        tail.Next = null;

        return tail;
    }

    public Link<T>? SpliceAfter()
    {
        if (Next is not Link<T> head)
        {
            return null;
        }

        Next = null;
        head.Previous = null;

        return head;
    }

    public void Prepend(SingleLink single)
    {
        if (single.Link is not Link<T> link)
        {
            return;
        }

        link.Previous = Previous;
        link.Next = this;

        if (Previous is not null)
        {
            Previous.Next = link;
        }

        Previous = link;
    }

    public readonly ref struct SingleLink
    {
        private readonly Link<T>? _link;

        public Link<T>? Link
        {
            get
            {
                if (_link is null)
                {
                    return null;
                }

                if (_link.Previous is not null)
                {
                    return null;
                }

                if (_link.Next is not null)
                {
                    return null;
                }

                return _link;
            }
        }

        internal SingleLink(Link<T> link)
        {
            _link = link;
        }
    }
}

internal static class SourceLinkExtensions
{
    public static Message? Cleanup(this Link<ISource> source, Message? root)
    {
        if (source.Value.Listener is not Message listener)
        {
            throw new InvalidOperationException("Source is missing listener");
        }

        listener.Restore();

        if (listener.IsUnused)
        {
            _ = source.Pop();
            source.Value.Untrack(listener);
        }
        else
        {
            if (root is { SourceLink: var rootSource })
            {
                rootSource.Prepend(source.Pop());
            }

            root = listener;
        }

        return root;
    }
}
