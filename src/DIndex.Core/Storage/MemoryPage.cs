using System.Runtime.InteropServices;
using DIndex.Core.Storage.Entities;

namespace DIndex.Core.Storage;

public sealed class MemoryPage
{
    public const int RecordSize = 216;
    public const int PageCapacity = 16_384;
    public const int PageSizeBytes = PageCapacity * RecordSize;

    private readonly byte[] _buffer;
    private int _count;

    public MemoryPage() => _buffer = new byte[PageSizeBytes];

    public int Count => _count;
    public bool IsFull => _count >= PageCapacity;

    public unsafe bool TryWrite(in Entity entity, out int localIndex)
    {
        if (IsFull)
        {
            localIndex = -1;
            return false;
        }

        localIndex = _count;
        int offset = _count * RecordSize;

        fixed (byte* dst = &_buffer[offset])
        fixed (Entity* src = &entity)
        {
            Buffer.MemoryCopy(src, dst, RecordSize, RecordSize);
        }

        _count++;
        return true;
    }

    public unsafe Entity Read(int localIndex)
    {
        if ((uint)localIndex >= (uint)_count)
            throw new IndexOutOfRangeException($"Локальний індекс {localIndex} виходить за межі [0, {_count})");

        int offset = localIndex * RecordSize;
        Entity entity;

        fixed (byte* src = &_buffer[offset])
        {
            Buffer.MemoryCopy(src, &entity, RecordSize, RecordSize);
        }

        return entity;
    }

    public unsafe void Update(int localIndex, in Entity entity)
    {
        if ((uint)localIndex >= (uint)_count)
            throw new IndexOutOfRangeException($"Локальний індекс {localIndex} виходить за межі [0, {_count})");

        int offset = localIndex * RecordSize;

        fixed (byte* dst = &_buffer[offset])
        fixed (Entity* src = &entity)
        {
            Buffer.MemoryCopy(src, dst, RecordSize, RecordSize);
        }
    }

    public Span<byte> GetRawSpan() => _buffer.AsSpan(0, _count * RecordSize);

    public void SetCount(int count)
    {
        if (count < 0 || count > PageCapacity)
            throw new ArgumentOutOfRangeException(nameof(count));

        _count = count;
    }

    public void LoadFromSpan(ReadOnlySpan<byte> src, int recordCount)
    {
        int bytes = recordCount * RecordSize;
        src.Slice(0, bytes).CopyTo(_buffer.AsSpan(0, bytes));
        _count = recordCount;
    }
}
