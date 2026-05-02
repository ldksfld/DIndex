namespace DIndex.Core.Indexing.LinkedList;

public sealed class DLinkedListNode<T>
{
    public T Value;
    public DLinkedListNode<T>? Prev;
    public DLinkedListNode<T>? Next;

    public DLinkedListNode(T value) => Value = value;
}
