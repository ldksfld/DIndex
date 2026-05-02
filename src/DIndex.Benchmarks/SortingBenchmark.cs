using BenchmarkDotNet.Attributes;
using DIndex.Core.Search;
using DIndex.Core.Storage.Entities;

namespace DIndex.Benchmarks;

[SimpleJob]
[MemoryDiagnoser]
[MarkdownExporter]
[HtmlExporter]
[CsvExporter]
public class SortingBenchmark
{
    private long[] _dataForCustom = null!;
    private long[] _dataForBuiltin = null!;

    [Params(10_000, 100_000, 1_000_000)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        _dataForCustom = new long[N];
        _dataForBuiltin = new long[N];

        for (int i = 0; i < N; i++)
            _dataForCustom[i] = _dataForBuiltin[i] = rng.NextInt64();
    }

    [Benchmark(Baseline = true, Description = "QuickSort (власний, медіана трьох)")]
    public void CustomQuickSort()
    {
        var copy = (long[])_dataForCustom.Clone();
        QuickSorter.Sort<long>(copy, (a, b) => a.CompareTo(b));
    }

    [Benchmark(Description = "Array.Sort (вбудований)")]
    public void BuiltinArraySort()
    {
        var copy = (long[])_dataForBuiltin.Clone();
        Array.Sort(copy);
    }
}
