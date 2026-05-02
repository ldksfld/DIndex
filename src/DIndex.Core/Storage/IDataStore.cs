using DIndex.Core.Storage.Entities;

namespace DIndex.Core.Storage;

public interface IDataStore
{
    int TotalCount { get; }
    int PageCount { get; }

    int Append(in Entity entity);
    Entity Read(int globalIndex);
    void Update(int globalIndex, in Entity entity);

    void ForEach(Action<int, Entity> action);
}
