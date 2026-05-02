using DIndex.Core.Search;

namespace DIndex.Core.Tests;

public sealed class QuickSortTests
{
    private static Comparison<long> LongCmp = (a, b) => a.CompareTo(b);

    [Fact]
    public void Sort_AlreadySorted_ResultIdentical()
    {
        long[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        var copy = (long[])data.Clone();

        QuickSorter.Sort<long>(copy, LongCmp);

        Assert.Equal(data, copy);
    }

    [Fact]
    public void Sort_ReverseSorted_Correct()
    {
        long[] data = [10, 9, 8, 7, 6, 5, 4, 3, 2, 1];

        QuickSorter.Sort<long>(data, LongCmp);

        for (int i = 0; i < data.Length - 1; i++)
            Assert.True(data[i] <= data[i + 1]);
    }

    [Fact]
    public void Sort_WithDuplicates_AllPreserved()
    {
        long[] data = [5, 3, 5, 1, 3, 2, 5];

        QuickSorter.Sort<long>(data, LongCmp);

        Assert.Equal(3, data.Count(x => x == 5));
        Assert.Equal(2, data.Count(x => x == 3));

        for (int i = 0; i < data.Length - 1; i++)
            Assert.True(data[i] <= data[i + 1]);
    }

    [Fact]
    public void Sort_LargeRandom_CorrectOrder()
    {
        var rng = new Random(123);
        var data = new long[10_000];

        for (int i = 0; i < data.Length; i++)
            data[i] = rng.NextInt64();

        QuickSorter.Sort<long>(data, LongCmp);

        for (int i = 0; i < data.Length - 1; i++)
            Assert.True(data[i] <= data[i + 1]);
    }

    [Fact]
    public void Sort_SingleElement_NoError()
    {
        long[] data = [42L];

        QuickSorter.Sort<long>(data, LongCmp);

        Assert.Equal(42L, data[0]);
    }

    [Fact]
    public void Sort_Empty_NoError()
    {
        long[] data = [];

        QuickSorter.Sort<long>(data, LongCmp);

        Assert.Empty(data);
    }

    [Fact]
    public void InterpolationSearch_UniformArray_FindsTarget()
    {
        long[] sorted = new long[1000];

        for (int i = 0; i < sorted.Length; i++)
            sorted[i] = i * 100L;

        int idx = InterpolationSearcher.Search(sorted, 5000L);

        Assert.Equal(50, idx);
    }

    [Fact]
    public void InterpolationSearch_NotFound_ReturnsMinusOne()
    {
        long[] sorted = [10L, 20L, 30L, 40L, 50L];

        int idx = InterpolationSearcher.Search(sorted, 25L);

        Assert.Equal(-1, idx);
    }

    [Fact]
    public void InterpolationSearch_FirstElement()
    {
        long[] sorted = [0L, 100L, 200L, 300L];

        Assert.Equal(0, InterpolationSearcher.Search(sorted, 0L));
    }

    [Fact]
    public void InterpolationSearch_LastElement()
    {
        long[] sorted = [0L, 100L, 200L, 300L];

        Assert.Equal(3, InterpolationSearcher.Search(sorted, 300L));
    }
}
