using BenchmarkDotNet.Attributes;
using DIndex.Core.Application;
using DIndex.Core.Indexing.Bst;
using DIndex.Core.Storage;
using DIndex.Core.Storage.Entities;

namespace DIndex.Benchmarks;

[SimpleJob]
[MemoryDiagnoser]
[MarkdownExporter]
[HtmlExporter]
[CsvExporter]
public class BstVsLinearBenchmark
{
    private BinarySearchTree _tree = null!;
    private long[] _keys = null!;
    private long _searchKey;

    [Params(1_000, 10_000, 100_000, 1_000_000)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        _tree = new BinarySearchTree();
        _keys = new long[N];
        var rng = new Random(42);

        for (int i = 0; i < N; i++)
        {
            long key = rng.NextInt64(1, long.MaxValue);
            _keys[i] = key;
            _tree.Insert(key, i);
        }

        _searchKey = _keys[rng.Next(N)];
    }

    [Benchmark(Baseline = true, Description = "BST Search O(log n)")]
    public bool BstSearch()
    {
        return _tree.TrySearch(_searchKey, out _);
    }

    [Benchmark(Description = "Linear Search O(n)")]
    public bool LinearSearch()
    {
        foreach (long k in _keys)
        {
            if (k == _searchKey)
                return true;
        }

        return false;
    }

    [GlobalCleanup]
    public void Cleanup() => _tree.Dispose();
}
