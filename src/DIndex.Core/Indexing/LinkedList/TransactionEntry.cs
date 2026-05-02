namespace DIndex.Core.Indexing.LinkedList;

public enum OperationType
{
    Insert,
    Delete,
    Update,
    Search
}

public sealed record TransactionEntry(
    OperationType Type,
    long EntityId,
    long Timestamp,
    string Description,
    byte[]? EntitySnapshot = null);
