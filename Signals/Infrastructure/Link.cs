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

    public void Pop()
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

    public void Prepend(Link<T> link)
    {
        if (link.Previous is not null)
        {
            throw new InvalidOperationException("Previous expected to be null");
        }

        if (link.Next is not null)
        {
            throw new InvalidOperationException("Next expected to be null");
        }

        link.Previous = Previous;
        link.Next = this;

        if (Previous is not null)
        {
            Previous.Next = link;
        }

        Previous = link;
    }

    // public void Prepend(Link<T> link)
    // {
    //     if (Previous is not null)
    //     {
    //         Previous.Next = null;
    //     }

    //     Previous = link;

    //     if (link.Next is not null)
    //     {
    //         link.Next.Previous = null;
    //     }

    //     link.Next = this;
    // }

    // public void Append(Link<T> link)
    // {
    //     if (Next is not null)
    //     {
    //         Next.Previous = null;
    //     }

    //     Next = link;

    //     if (link.Previous is not null)
    //     {
    //         link.Previous.Next = null;
    //     }

    //     link.Previous = this;
    // }
}
