using DIndex.Core.Storage.Entities;

namespace DIndex.Core.Storage.Serialization;

public sealed class BinarySnapshotWriter
{
    private const uint MagicNumber = 0x44494458u;
    private const ushort Version = 1;
    private const uint EofMarker = 0xDEADBEEFu;
    private const int BufferSize = 64 * 1024;

    public bool Write(
        string filePath,
        PagedMemoryStore store,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
        using var bw = new BinaryWriter(fs);

        long activeCount = CountActive(store);
        long createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var hdrBuf = new byte[24];

        using (var hms = new MemoryStream(hdrBuf))
        using (var hbw = new BinaryWriter(hms))
        {
            hbw.Write(MagicNumber);
            hbw.Write(Version);
            hbw.Write((ushort)0);
            hbw.Write(activeCount);
            hbw.Write(createdAt);
        }

        uint headerCrc = Crc32.Compute(hdrBuf);

        bw.Write(MagicNumber);
        bw.Write(Version);
        bw.Write((ushort)0);
        bw.Write(activeCount);
        bw.Write(createdAt);
        bw.Write(headerCrc);

        int written = 0;
        bool cancelled = false;

        store.ForEach((idx, entity) =>
        {
            if (cancelled || ct.IsCancellationRequested)
            {
                cancelled = true;
                return;
            }

            if (entity.IsDeleted)
                return;

            entity.UpdateChecksum();
            WriteEntity(bw, in entity);

            written++;
            progress?.Report((int)((long)written * 100 / (activeCount == 0 ? 1 : activeCount)));
        });

        if (!cancelled)
            bw.Write(EofMarker);

        bw.Flush();
        return !cancelled;
    }

    private static long CountActive(PagedMemoryStore store)
    {
        long c = 0;
        store.ForEach((_, e) =>
        {
            if (!e.IsDeleted)
                c++;
        });
        return c;
    }

    private static unsafe void WriteEntity(BinaryWriter bw, in Entity entity)
    {
        fixed (Entity* p = &entity)
        {
            var span = new ReadOnlySpan<byte>(p, MemoryPage.RecordSize);
            bw.Write(span);
        }
    }
}
