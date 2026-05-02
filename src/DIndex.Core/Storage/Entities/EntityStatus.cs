namespace DIndex.Core.Storage.Entities;

public enum EntityStatus : byte
{
    Active = 0,
    Deleted = 1,
    Locked = 2,
}
