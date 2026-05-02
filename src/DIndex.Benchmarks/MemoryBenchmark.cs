using BenchmarkDotNet.Attributes;
using DIndex.Core.Application;
using DIndex.Core.Storage.Entities;

namespace DIndex.Benchmarks;

[SimpleJob]
[MemoryDiagnoser]
[MarkdownExporter]
[HtmlExporter]
[CsvExporter]
public class MemoryBenchmark
{
    private DataEngine _engine = null!;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new DataEngine();
    }

    [Benchmark(Description = "Insert Single Record (unsafe Entity, очікується 0 heap-алокацій на ядро)")]
    public long InsertRecord()
    {
        return _engine.AddRecord("bench@test.com", "benchmark-data");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _engine.Dispose();
    }
}
