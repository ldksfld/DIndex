namespace DIndex.Core.Indexing;

public interface IIndex
{
    int  Count { get; }
    void Insert(long key, int storeIndex);
    bool TrySearch(long key, out int storeIndex);
    bool Delete(long key);
    void Clear();
    int  Height();
}
