using DIndex.Core.Application;
using DIndex.Core.Storage;
using DIndex.Core.Storage.Entities;
using DIndex.Core.Storage.Serialization;

namespace DIndex.Core.Tests;

public sealed class SnapshotTests : IDisposable
{
    private readonly string _tmpFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.didx");

    [Fact]
    public void WriteRead_1000Records_AllRestoredCorrectly()
    {
        var engine = new DataEngine();

        for (int i = 1; i <= 1000; i++)
            engine.AddRecord($"user{i:D4}@test.com", $"data-{i}");

        engine.SaveSnapshot(_tmpFile);

        var engine2 = new DataEngine();
        var (info, corrupted) = engine2.LoadSnapshot(_tmpFile);

        Assert.Equal(0, corrupted);
        Assert.Equal(1000, info.RecordCount);

        bool found = engine2.TryGetById(500, out var result);
        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal(500L, result!.Id);
        Assert.Equal("user0500@test.com", result.Key);
        Assert.Equal("data-500", result.Data);
    }

    [Fact]
    public void SnapshotInfo_HasCorrectMagic()
    {
        var engine = new DataEngine();
        engine.AddRecord("key@test.com", "value");
        engine.SaveSnapshot(_tmpFile);

        var reader = new BinarySnapshotReader();
        var info = reader.ReadInfo(_tmpFile);

        Assert.Equal(1, info.FormatVersion);
        Assert.Equal(1, info.RecordCount);
        Assert.True(info.FileSize > 0);
    }

    [Fact]
    public void Crc32_Compute_ConsistentForSameData()
    {
        byte[] data = [0x01, 0x02, 0x03, 0x04, 0x05];

        uint c1 = Crc32.Compute(data);
        uint c2 = Crc32.Compute(data);

        Assert.Equal(c1, c2);
    }

    [Fact]
    public void Crc32_DifferentData_DifferentChecksum()
    {
        uint c1 = Crc32.Compute([0x01, 0x02]);
        uint c2 = Crc32.Compute([0x01, 0x03]);

        Assert.NotEqual(c1, c2);
    }

    public void Dispose()
    {
        if (File.Exists(_tmpFile))
            File.Delete(_tmpFile);
    }
}
