using System.Runtime.InteropServices;
using System.Text;
using DIndex.Core.Storage.Serialization;

namespace DIndex.Core.Storage.Entities;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Entity
{
    public long Id;
    public long Timestamp;
    public byte Flags;
    public byte KeyLength;
    public fixed byte Key[64];
    public ushort DataLength;
    public fixed byte Data[128];
    public uint Checksum;

    public static int Size => sizeof(Entity);

    public void SetKey(ReadOnlySpan<char> value)
    {
        Span<byte> buf = stackalloc byte[64];
        Encoding.UTF8.GetEncoder().Convert(value, buf, flush: true, out _, out int written, out _);
        KeyLength = (byte)written;

        fixed (byte* dst = Key)
        {
            buf.Slice(0, written).CopyTo(new Span<byte>(dst, 64));
        }
    }

    public string GetKey()
    {
        fixed (byte* src = Key)
        {
            return Encoding.UTF8.GetString(src, KeyLength);
        }
    }

    public void SetData(ReadOnlySpan<char> value)
    {
        Span<byte> buf = stackalloc byte[128];
        Encoding.UTF8.GetEncoder().Convert(value, buf, flush: true, out _, out int written, out _);
        DataLength = (ushort)written;

        fixed (byte* dst = Data)
        {
            buf.Slice(0, written).CopyTo(new Span<byte>(dst, 128));
        }
    }

    public string GetData()
    {
        fixed (byte* src = Data)
        {
            return Encoding.UTF8.GetString(src, DataLength);
        }
    }

    public bool IsActive => EntityFlags.HasFlag(Flags, EntityFlags.IsActive);
    public bool IsDeleted => EntityFlags.HasFlag(Flags, EntityFlags.IsDeleted);
    public bool IsLocked => EntityFlags.HasFlag(Flags, EntityFlags.IsLocked);
    public bool IsIndexed => EntityFlags.HasFlag(Flags, EntityFlags.IsIndexed);

    public uint ComputeChecksum()
    {
        fixed (Entity* ptr = &this)
        {
            var span = new ReadOnlySpan<byte>(ptr, 212);
            return Crc32.Compute(span);
        }
    }

    public void UpdateChecksum() => Checksum = ComputeChecksum();

    public bool IsChecksumValid() => Checksum == ComputeChecksum();
}
