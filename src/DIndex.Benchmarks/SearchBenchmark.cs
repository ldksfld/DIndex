using BenchmarkDotNet.Attributes;
using DIndex.Core.Search;

namespace DIndex.Benchmarks;

[SimpleJob]
[MemoryDiagnoser]
[MarkdownExporter]
[HtmlExporter]
[CsvExporter]
public class SearchBenchmark
{
    private long[] _sortedKeys = null!;
    private long _target;

    [Params(1_000, 10_000, 100_000, 1_000_000)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        _sortedKeys = new long[N];

        for (int i = 0; i < N; i++)
            _sortedKeys[i] = (long)i * 1000;

        _target = _sortedKeys[N / 2];
    }

    [Benchmark(Baseline = true, Description = "InterpolationSearch O(log log n)")]
    public int CustomInterpolation()
    {
        return InterpolationSearcher.Search(_sortedKeys, _target);
    }

    [Benchmark(Description = "Array.BinarySearch O(log n)")]
    public int BuiltinBinarySearch()
    {
        return Array.BinarySearch(_sortedKeys, _target);
    }
}
