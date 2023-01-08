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

    public Single Pop()
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

        return new Single(this);
    }

    public Link<T>? SpliceBefore()
    {
        if (Previous is not Link<T> last)
        {
            return null;
        }

        Previous = null;
        last.Next = null;

        return last;
    }

    public Link<T>? SpliceAfter()
    {
        if (Next is not Link<T> first)
        {
            return null;
        }

        Next = null;
        first.Previous = null;

        return first;
    }

    public void Prepend(Single single)
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

    public void Append(Single single)
    {
        if (single.Link is not Link<T> link)
        {
            return;
        }

        link.Previous = this;
        link.Next = Next;

        if (Next is not null)
        {
            Next.Previous = link;
        }

        Next = link;
    }

    internal readonly ref struct Single
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

        internal Single(Link<T> link)
        {
            _link = link;
        }
    }
}
