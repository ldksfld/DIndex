namespace DIndex.Core.Storage.Entities;

public static class EntityFlags
{
    public const byte IsActive = 0x01;
    public const byte IsDeleted = 0x02;
    public const byte IsLocked = 0x04;
    public const byte IsIndexed = 0x08;

    public static void SetFlag(ref byte flags, byte mask) => flags |= mask;

    public static void ClearFlag(ref byte flags, byte mask) => flags &= (byte)~mask;

    public static bool HasFlag(byte flags, byte mask) => (flags & mask) != 0;
}
