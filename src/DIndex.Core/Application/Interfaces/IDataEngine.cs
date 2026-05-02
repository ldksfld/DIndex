using DIndex.Core.Indexing.LinkedList;
using DIndex.Core.Search;
using DIndex.Core.Storage.Serialization;

namespace DIndex.Core.Application.Interfaces;

public interface IDataEngine
{
    int TotalCount { get; }
    int ActiveCount { get; }
    int BstHeight { get; }
    bool IsIndexDeep { get; }
    int PageCount { get; }
    long LastSearchMs { get; }
    int LogCount { get; }

    long AddRecord(string key, string data, long? id = null);
    bool TryGetById(long id, out SearchResult? result);
    bool DeleteRecord(long id);
    bool UpdateRecord(long id, string newData);

    SearchResult[] SearchByRange(long minId, long maxId);
    SearchResult[] SearchByKeyPrefix(string prefix);
    SearchResult[] GetActiveRecordsPage(int skip, int take);

    bool SaveSnapshot(string filePath, IProgress<int>? progress = null, CancellationToken ct = default);

    (SnapshotInfo info, int corrupted) LoadSnapshot(
        string filePath,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    void ClearAll();

    TransactionEntry? Undo();
    TransactionEntry? Redo();
    bool CanUndo { get; }
    bool CanRedo { get; }
    TransactionEntry[] GetTransactionLog();
    void ClearTransactionLog();

    (long Timestamp, int Count)[] GetTimeSeries(int maxBuckets = 12);
}
