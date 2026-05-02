using DIndex.Core.Indexing.LinkedList;

namespace DIndex.Core.Tests;

public sealed class LinkedListTests
{
    [Fact]
    public void AddLast100_CountIs100()
    {
        var list = new DoublyLinkedList<int>();

        for (int i = 0; i < 100; i++)
            list.AddLast(i);

        Assert.Equal(100, list.Count);
    }

    [Fact]
    public void RemoveFirst_HeadMovesToNext()
    {
        var list = new DoublyLinkedList<int>();
        list.AddLast(1);
        list.AddLast(2);
        list.AddLast(3);

        list.RemoveFirst(out int val);

        Assert.Equal(1, val);
        Assert.Equal(2, list.Head!.Value);
    }

    [Fact]
    public void RemoveLast_TailMovesToPrev()
    {
        var list = new DoublyLinkedList<int>();
        list.AddLast(1);
        list.AddLast(2);
        list.AddLast(3);

        list.RemoveLast(out int val);

        Assert.Equal(3, val);
        Assert.Equal(2, list.Tail!.Value);
    }

    [Fact]
    public void ToArray_PreservesOrder()
    {
        var list = new DoublyLinkedList<int>();

        for (int i = 0; i < 10; i++)
            list.AddLast(i);

        var arr = list.ToArray();

        Assert.Equal(10, arr.Length);

        for (int i = 0; i < 10; i++)
            Assert.Equal(i, arr[i]);
    }

    [Fact]
    public void Find_ReturnsCorrectNode()
    {
        var list = new DoublyLinkedList<string>();
        list.AddLast("alpha");
        list.AddLast("beta");
        list.AddLast("gamma");

        var node = list.Find(s => s == "beta");

        Assert.NotNull(node);
        Assert.Equal("beta", node!.Value);
    }

    [Fact]
    public void TransactionLog_Over100_DropsOldest()
    {
        var log = new TransactionLog();

        for (int i = 0; i < 101; i++)
        {
            log.Record(new TransactionEntry(
                OperationType.Insert,
                i,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                $"op{i}"));
        }

        Assert.Equal(100, log.Count);
    }

    [Fact]
    public void TransactionLog_Undo_ReturnsLastEntry()
    {
        var log = new TransactionLog();
        log.Record(new TransactionEntry(OperationType.Insert, 1, 0, "first"));
        log.Record(new TransactionEntry(OperationType.Insert, 2, 0, "second"));

        var entry = log.Undo();

        Assert.NotNull(entry);
        Assert.Equal(2, entry!.EntityId);
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void TransactionLog_Redo_AfterUndo()
    {
        var log = new TransactionLog();
        log.Record(new TransactionEntry(OperationType.Insert, 99, 0, "test"));
        log.Undo();

        Assert.True(log.CanRedo);

        var entry = log.Redo();

        Assert.NotNull(entry);
        Assert.Equal(99, entry!.EntityId);
    }

    [Fact]
    public void AddFirst_HeadChanges()
    {
        var list = new DoublyLinkedList<int>();
        list.AddLast(2);
        list.AddFirst(1);

        Assert.Equal(1, list.Head!.Value);
        Assert.Equal(2, list.Count);
    }
}
