namespace DIndex.Core.Indexing.Bst;

public sealed class BstNode
{
    public long Key;
    public int StoreIndex;
    public BstNode? Left;
    public BstNode? Right;

    public BstNode(long key, int storeIndex)
    {
        Key = key;
        StoreIndex = storeIndex;
    }
}
