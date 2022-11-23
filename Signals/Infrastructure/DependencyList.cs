using System.Collections;

namespace Signals.Infrastructure;

internal sealed class DependencyList : IEnumerable<Message>
{
    private LinkedList<Message>? _dependencies;

    public Message? First => _dependencies?.First?.Value;

    public void Prepend(LinkedListNode<Message> node)
    {
        if (node.List is LinkedList<Message> oldList)
        {
            oldList.Remove(node);
        }

        _dependencies ??= new LinkedList<Message>();
        _dependencies.AddFirst(node);
    }

    public void Prepend(Message message)
    {
        _dependencies ??= new LinkedList<Message>();
        _dependencies.AddFirst(message);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IEnumerator<Message> GetEnumerator()
    {
        if (_dependencies == null)
        {
            yield break;
        }

        foreach (var dep in _dependencies)
        {
            yield return dep;
        }
    }
}
