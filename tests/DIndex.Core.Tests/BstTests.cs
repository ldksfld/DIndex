using DIndex.Core.Indexing.Bst;

namespace DIndex.Core.Tests;

public sealed class BstTests : IDisposable
{
    private readonly BinarySearchTree _tree = new();

    [Fact]
    public void Insert1000RandomKeys_InOrderIsAscending()
    {
        var rng = new Random(42);
        var keys = new long[1000];

        for (int i = 0; i < 1000; i++)
        {
            keys[i] = rng.NextInt64(1, 1_000_000);
            _tree.Insert(keys[i], i);
        }

        long prev = long.MinValue;

        _tree.InOrderTraversal((k, _) =>
        {
            Assert.True(k >= prev, $"In-order порушено: {k} < {prev}");
            prev = k;
        });
    }

    [Fact]
    public void Search_AfterInsert_ReturnsCorrectStoreIndex()
    {
        _tree.Insert(42L, 7);

        Assert.True(_tree.TrySearch(42L, out int idx));
        Assert.Equal(7, idx);
    }

    [Fact]
    public void Search_NotExisting_ReturnsFalse()
    {
        _tree.Insert(10L, 0);
        Assert.False(_tree.TrySearch(999L, out _));
    }

    [Fact]
    public void Delete_NodeWithTwoChildren_TreeRemainsValidBst()
    {
        foreach (var (k, i) in new[] { (5L, 0), (3L, 1), (7L, 2), (2L, 3), (4L, 4), (6L, 5), (8L, 6) })
            _tree.Insert(k, i);

        bool deleted = _tree.Delete(3L);
        Assert.True(deleted);

        long prev = long.MinValue;

        _tree.InOrderTraversal((k, _) =>
        {
            Assert.True(k > prev);
            prev = k;
        });

        Assert.Equal(6, _tree.Count);
    }

    [Fact]
    public void Clear_SetsCountToZero()
    {
        for (int i = 0; i < 100; i++)
            _tree.Insert(i, i);

        _tree.Clear();
        Assert.Equal(0, _tree.Count);
    }

    [Fact]
    public void Height_SingleNode_ReturnsOne()
    {
        _tree.Insert(1L, 0);
        Assert.Equal(1, _tree.Height());
    }

    [Fact]
    public void RangeSearch_ReturnsOnlyInRange()
    {
        for (int i = 1; i <= 20; i++)
            _tree.Insert(i, i);

        var found = new System.Collections.Generic.List<long>();
        _tree.RangeSearch(5, 10, (k, _) => found.Add(k));

        Assert.Equal(6, found.Count);
        Assert.All(found, k => Assert.True(k >= 5 && k <= 10));
    }

    [Fact]
    public void PreOrder_RootFirst()
    {
        _tree.Insert(10L, 0);
        _tree.Insert(5L, 1);
        _tree.Insert(15L, 2);

        var result = new System.Collections.Generic.List<long>();
        _tree.PreOrderTraversal((k, _) => result.Add(k));

        Assert.Equal(10L, result[0]);
    }

    [Fact]
    public void PostOrder_RootLast()
    {
        _tree.Insert(10L, 0);
        _tree.Insert(5L, 1);
        _tree.Insert(15L, 2);

        var result = new System.Collections.Generic.List<long>();
        _tree.PostOrderTraversal((k, _) => result.Add(k));

        Assert.Equal(10L, result[^1]);
    }

    public void Dispose() => _tree.Dispose();
}
