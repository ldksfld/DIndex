using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using DIndex.Core.Storage.Entities;

namespace DIndex.Core.Storage.Serialization;

public sealed class BinarySnapshotReader
{
    private const uint MagicNumber = 0x44494458u;
    private const ushort SupportedVersion = 1;
    private const uint EofMarker = 0xDEADBEEFu;

    public SnapshotInfo ReadInfo(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs);
        return ReadHeader(br, filePath);
    }

    public (SnapshotInfo info, int corrupted) Load(
        string filePath,
        PagedMemoryStore store,
        Action<long, int>? onRecord = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        store.Reset();

        using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        using var view = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
        using var br = new BinaryReader(view);

        var info = ReadHeader(br, filePath);
        int corrupted = 0;
        int loaded = 0;

        bool cancelled = false;

        for (long i = 0; i < info.RecordCount; i++)
        {
            if (ct.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            var entity = ReadEntity(br);

            if (!entity.IsChecksumValid())
            {
                corrupted++;
                continue;
            }

            int globalIndex = store.AppendDirect(entity);
            onRecord?.Invoke(entity.Id, globalIndex);

            loaded++;
            progress?.Report((int)((long)loaded * 100 / (info.RecordCount == 0 ? 1 : info.RecordCount)));
        }

        if (!cancelled)
        {
            uint eof = br.ReadUInt32();

            if (eof != EofMarker)
                throw new InvalidDataException("Відсутній або пошкоджений EOF-маркер .didx файлу.");
        }

        return (info with { RecordCount = loaded }, corrupted);
    }

    private static SnapshotInfo ReadHeader(BinaryReader br, string filePath)
    {
        uint magic = br.ReadUInt32();

        if (magic != MagicNumber)
            throw new InvalidDataException($"Невірний Magic Number: {magic:X8}. Файл не є .didx.");

        ushort version = br.ReadUInt16();

        if (version != SupportedVersion)
            throw new NotSupportedException($"Непідтримувана версія формату: {version}.");

        ushort flags = br.ReadUInt16();
        long count = br.ReadInt64();
        long createdAt = br.ReadInt64();
        uint headerCrc = br.ReadUInt32();

        var hdrBuf = new byte[24];

        using (var ms = new MemoryStream(hdrBuf))
        using (var hw = new BinaryWriter(ms))
        {
            hw.Write(MagicNumber);
            hw.Write(version);
            hw.Write(flags);
            hw.Write(count);
            hw.Write(createdAt);
        }

        uint expectedCrc = Crc32.Compute(hdrBuf);

        if (headerCrc != expectedCrc)
            throw new InvalidDataException("Пошкоджений CRC заголовка .didx файлу.");

        var fi = new FileInfo(filePath);

        return new SnapshotInfo(
            filePath,
            fi.Length,
            DateTimeOffset.FromUnixTimeSeconds(createdAt),
            count,
            version);
    }

    private static unsafe Entity ReadEntity(BinaryReader br)
    {
        Entity entity;
        var span = new Span<byte>(&entity, MemoryPage.RecordSize);
        int read = br.Read(span);

        if (read < MemoryPage.RecordSize)
            throw new EndOfStreamException("Несподіваний кінець .didx файлу при читанні запису.");

        return entity;
    }
}

public sealed record SnapshotInfo(
    string FilePath,
    long FileSize,
    DateTimeOffset CreatedAt,
    long RecordCount,
    ushort FormatVersion);
