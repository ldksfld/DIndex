using DIndex.Core.Storage.Entities;

namespace DIndex.Core.Tests;

public sealed class EntityFlagsTests
{
    [Fact]
    public void SetFlag_SetsCorrectBit()
    {
        byte flags = 0;
        EntityFlags.SetFlag(ref flags, EntityFlags.IsActive);

        Assert.True(EntityFlags.HasFlag(flags, EntityFlags.IsActive));
    }

    [Fact]
    public void ClearFlag_ClearsCorrectBit()
    {
        byte flags = 0xFF;
        EntityFlags.ClearFlag(ref flags, EntityFlags.IsDeleted);

        Assert.False(EntityFlags.HasFlag(flags, EntityFlags.IsDeleted));
    }

    [Fact]
    public void SetMultipleFlags_IndependentBits()
    {
        byte flags = 0;
        EntityFlags.SetFlag(ref flags, EntityFlags.IsActive);
        EntityFlags.SetFlag(ref flags, EntityFlags.IsIndexed);

        Assert.True(EntityFlags.HasFlag(flags, EntityFlags.IsActive));
        Assert.True(EntityFlags.HasFlag(flags, EntityFlags.IsIndexed));
        Assert.False(EntityFlags.HasFlag(flags, EntityFlags.IsDeleted));
        Assert.False(EntityFlags.HasFlag(flags, EntityFlags.IsLocked));
    }

    [Fact]
    public void ClearFlag_DoesNotAffectOtherBits()
    {
        byte flags = 0;
        EntityFlags.SetFlag(ref flags, EntityFlags.IsActive);
        EntityFlags.SetFlag(ref flags, EntityFlags.IsIndexed);
        EntityFlags.ClearFlag(ref flags, EntityFlags.IsActive);

        Assert.False(EntityFlags.HasFlag(flags, EntityFlags.IsActive));
        Assert.True(EntityFlags.HasFlag(flags, EntityFlags.IsIndexed));
    }

    [Fact]
    public void EntitySize_Is216Bytes()
    {
        unsafe
        {
            Assert.Equal(216, sizeof(Entity));
        }
    }

    [Fact]
    public unsafe void Entity_SetKey_GetKey_Roundtrip()
    {
        Entity e = default;
        e.SetKey("test@example.com");

        Assert.Equal("test@example.com", e.GetKey());
    }

    [Fact]
    public unsafe void Entity_SetData_GetData_Roundtrip()
    {
        Entity e = default;
        e.SetData("payload-12345");

        Assert.Equal("payload-12345", e.GetData());
    }

    [Fact]
    public unsafe void Entity_Checksum_ValidAfterUpdate()
    {
        Entity e = default;
        e.Id = 42;
        e.SetKey("user@test.com");
        e.SetData("data");
        EntityFlags.SetFlag(ref e.Flags, EntityFlags.IsActive);
        e.UpdateChecksum();

        Assert.True(e.IsChecksumValid());
    }

    [Fact]
    public unsafe void Entity_Checksum_InvalidAfterModify()
    {
        Entity e = default;
        e.Id = 42;
        e.UpdateChecksum();
        e.Id = 99;

        Assert.False(e.IsChecksumValid());
    }
}
