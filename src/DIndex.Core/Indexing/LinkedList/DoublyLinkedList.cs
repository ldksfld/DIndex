namespace DIndex.Core.Indexing.LinkedList;

public sealed class DoublyLinkedList<T>
{
    private DLinkedListNode<T>? _head;
    private DLinkedListNode<T>? _tail;
    private int _count;

    public int Count => _count;
    public DLinkedListNode<T>? Head => _head;
    public DLinkedListNode<T>? Tail => _tail;

    public void AddFirst(T value)
    {
        var node = new DLinkedListNode<T>(value);

        if (_head is null)
        {
            _head = _tail = node;
        }
        else
        {
            node.Next = _head;
            _head.Prev = node;
            _head = node;
        }

        _count++;
    }

    public void AddLast(T value)
    {
        var node = new DLinkedListNode<T>(value);

        if (_tail is null)
        {
            _head = _tail = node;
        }
        else
        {
            node.Prev = _tail;
            _tail.Next = node;
            _tail = node;
        }

        _count++;
    }

    public bool RemoveFirst(out T value)
    {
        if (_head is null)
        {
            value = default!;
            return false;
        }

        value = _head.Value;
        _head = _head.Next;

        if (_head is not null)
            _head.Prev = null;
        else
            _tail = null;

        _count--;
        return true;
    }

    public bool RemoveLast(out T value)
    {
        if (_tail is null)
        {
            value = default!;
            return false;
        }

        value = _tail.Value;
        _tail = _tail.Prev;

        if (_tail is not null)
            _tail.Next = null;
        else
            _head = null;

        _count--;
        return true;
    }

    public DLinkedListNode<T>? Find(Predicate<T> match)
    {
        var cur = _head;

        while (cur is not null)
        {
            if (match(cur.Value))
                return cur;
            cur = cur.Next;
        }

        return null;
    }

    public T[] ToArray()
    {
        var result = new T[_count];
        var cur = _head;

        for (int i = 0; i < _count; i++)
        {
            result[i] = cur!.Value;
            cur = cur.Next;
        }

        return result;
    }

    public T[] ToArrayReversed()
    {
        var result = new T[_count];
        var cur = _tail;

        for (int i = 0; i < _count; i++)
        {
            result[i] = cur!.Value;
            cur = cur.Prev;
        }

        return result;
    }

    public void Clear()
    {
        _head = _tail = null;
        _count = 0;
    }
}
