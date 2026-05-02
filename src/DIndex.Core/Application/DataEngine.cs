using System.Diagnostics;
using DIndex.Core.Application.Interfaces;
using DIndex.Core.Indexing.Bst;
using DIndex.Core.Indexing.LinkedList;
using DIndex.Core.Search;
using DIndex.Core.Storage;
using DIndex.Core.Storage.Entities;
using DIndex.Core.Storage.Serialization;

namespace DIndex.Core.Application;

public sealed class DataEngine : IDataEngine, IDisposable
{
    private readonly PagedMemoryStore _store;
    private readonly BinarySearchTree _index;
    private readonly TransactionLog _log;
    private readonly BinarySnapshotWriter _writer;
    private readonly BinarySnapshotReader _reader;

    private long _nextId;
    private long _activeCount;
    private long _lastSearchMs;

    public DataEngine()
    {
        _store = new PagedMemoryStore();
        _index = new BinarySearchTree();
        _log = new TransactionLog();
        _writer = new BinarySnapshotWriter();
        _reader = new BinarySnapshotReader();
    }

    public int TotalCount => _store.TotalCount;
    public int ActiveCount => (int)Volatile.Read(ref _activeCount);
    public int BstHeight => _index.Height();
    public bool IsIndexDeep => _index.IsDeep;
    public int PageCount => _store.PageCount;
    public long LastSearchMs => Volatile.Read(ref _lastSearchMs);
    public int LogCount => _log.Count;

    public long AddRecord(string key, string data, long? id = null)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Ключ не може бути порожнім.", nameof(key));

        long entityId = id ?? Interlocked.Increment(ref _nextId);

        if (id.HasValue && _index.TrySearch(entityId, out _))
            throw new InvalidOperationException($"Запис з Id={entityId} вже існує.");

        unsafe
        {
            Entity entity = default;
            entity.Id = entityId;
            entity.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            entity.SetKey(key);
            entity.SetData(data);
            EntityFlags.SetFlag(ref entity.Flags, EntityFlags.IsActive);
            entity.UpdateChecksum();

            int globalIndex = _store.Append(entity);

            EntityFlags.SetFlag(ref entity.Flags, EntityFlags.IsIndexed);
            entity.UpdateChecksum();
            _store.Update(globalIndex, entity);

            _index.Insert(entityId, globalIndex);
        }

        Interlocked.Increment(ref _activeCount);

        if (id.HasValue && id.Value > _nextId)
            Interlocked.Exchange(ref _nextId, id.Value);

        _log.Record(new TransactionEntry(
            OperationType.Insert,
            entityId,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            $"Додано запис Id={entityId}, Key={key[..Math.Min(key.Length, 20)]}"));

        return entityId;
    }

    public bool TryGetById(long id, out SearchResult? result)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!_index.TrySearch(id, out int gi))
            {
                result = null;
                return false;
            }

            var e = _store.Read(gi);

            if (e.IsDeleted)
            {
                result = null;
                return false;
            }

            result = EntityToResult(e, gi);
            return true;
        }
        finally
        {
            Volatile.Write(ref _lastSearchMs, sw.ElapsedMilliseconds);
        }
    }

    public bool DeleteRecord(long id)
    {
        if (!_index.TrySearch(id, out int gi))
            return false;

        var e = _store.Read(gi);

        if (e.IsDeleted)
            return false;

        byte[] snapshot = EntityToBytes(in e);

        EntityFlags.SetFlag(ref e.Flags, EntityFlags.IsDeleted);
        EntityFlags.ClearFlag(ref e.Flags, EntityFlags.IsActive);
        e.UpdateChecksum();
        _store.Update(gi, e);

        _index.Delete(id);
        Interlocked.Decrement(ref _activeCount);

        _log.Record(new TransactionEntry(
            OperationType.Delete,
            id,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            $"Видалено Id={id}",
            snapshot));

        return true;
    }

    public bool UpdateRecord(long id, string newData)
    {
        if (!_index.TrySearch(id, out int gi))
            return false;

        var e = _store.Read(gi);

        if (e.IsDeleted)
            return false;

        e.SetData(newData);
        e.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        e.UpdateChecksum();
        _store.Update(gi, e);

        _log.Record(new TransactionEntry(
            OperationType.Update,
            id,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            $"Оновлено Id={id}"));

        return true;
    }

    public SearchResult[] GetActiveRecordsPage(int skip, int take)
    {
        if (skip < 0) skip = 0;
        if (take <= 0) return [];

        int initialCapacity = Math.Min(take, 64);
        var results = new DynamicArray<SearchResult>(initialCapacity);
        int matched = 0;
        bool stop = false;

        _store.ForEach((gi, e) =>
        {
            if (stop || e.IsDeleted)
                return;

            if (matched >= skip)
            {
                results.Add(EntityToResult(e, gi));
                if (results.Count >= take)
                    stop = true;
            }

            matched++;
        });

        return results.ToArray();
    }

    public SearchResult[] SearchByRange(long minId, long maxId)
    {
        var sw = Stopwatch.StartNew();
        var results = new DynamicArray<SearchResult>(64);

        try
        {
            _index.RangeSearch(minId, maxId, (key, gi) =>
            {
                var e = _store.Read(gi);
                if (!e.IsDeleted)
                    results.Add(EntityToResult(e, gi));
            });
        }
        finally
        {
            Volatile.Write(ref _lastSearchMs, sw.ElapsedMilliseconds);
        }

        return results.ToArray();
    }

    public SearchResult[] SearchByKeyPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            throw new ArgumentException("Префікс ключа не може бути порожнім.", nameof(prefix));

        var sw = Stopwatch.StartNew();
        var results = new DynamicArray<SearchResult>(32);

        _store.ForEach((gi, e) =>
        {
            if (e.IsDeleted) return;

            string key = e.GetKey();
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                results.Add(EntityToResult(e, gi));
        });

        Volatile.Write(ref _lastSearchMs, sw.ElapsedMilliseconds);
        return results.ToArray();
    }

    public bool SaveSnapshot(string filePath, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        return _writer.Write(filePath, _store, progress, ct);
    }

    public (SnapshotInfo info, int corrupted) LoadSnapshot(
        string filePath,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        _log.Clear();
        _index.Clear();

        var (info, corrupted) = _reader.Load(
            filePath,
            _store,
            (entityId, gi) => _index.Insert(entityId, gi),
            progress,
            ct);

        long active = 0;
        long maxId = 0;

        _store.ForEach((_, e) =>
        {
            if (!e.IsDeleted)
            {
                active++;
                if (e.Id > maxId) maxId = e.Id;
            }
        });

        Interlocked.Exchange(ref _activeCount, active);
        Interlocked.Exchange(ref _nextId, maxId);

        return (info, corrupted);
    }

    public void ClearAll()
    {
        _index.Clear();
        _store.Reset();
        Interlocked.Exchange(ref _activeCount, 0);
        Interlocked.Exchange(ref _nextId, 0);
        _log.Clear();
    }

    public bool CanUndo => _log.CanUndo;
    public bool CanRedo => _log.CanRedo;

    public TransactionEntry? Undo()
    {
        var peek = _log.PeekLast();
        if (peek is null) return null;

        byte[]? snapshotForRedo = null;

        if (peek.Type == OperationType.Insert && _index.TrySearch(peek.EntityId, out int gi))
        {
            var e = _store.Read(gi);
            if (!e.IsDeleted)
                snapshotForRedo = EntityToBytes(in e);
        }

        var entry = _log.Undo(snapshotForRedo);
        if (entry is null) return null;

        switch (entry.Type)
        {
            case OperationType.Insert:
                DeleteRecordDirect(entry.EntityId);
                break;

            case OperationType.Delete:
                if (entry.EntitySnapshot is not null)
                    RestoreRecordDirect(entry.EntitySnapshot);
                break;
        }

        return entry;
    }

    public TransactionEntry? Redo()
    {
        var entry = _log.Redo();
        if (entry is null) return null;

        switch (entry.Type)
        {
            case OperationType.Insert:
                if (entry.EntitySnapshot is not null)
                    RestoreRecordDirect(entry.EntitySnapshot);
                break;

            case OperationType.Delete:
                DeleteRecordDirect(entry.EntityId);
                break;
        }

        return entry;
    }

    private void DeleteRecordDirect(long id)
    {
        if (!_index.TrySearch(id, out int gi)) return;

        var e = _store.Read(gi);
        if (e.IsDeleted) return;

        EntityFlags.SetFlag(ref e.Flags, EntityFlags.IsDeleted);
        EntityFlags.ClearFlag(ref e.Flags, EntityFlags.IsActive);
        e.UpdateChecksum();
        _store.Update(gi, e);

        _index.Delete(id);
        Interlocked.Decrement(ref _activeCount);
    }

    private static unsafe byte[] EntityToBytes(in Storage.Entities.Entity e)
    {
        var bytes = new byte[sizeof(Storage.Entities.Entity)];

        fixed (Storage.Entities.Entity* pSrc = &e)
        fixed (byte* pDst = bytes)
        {
            Buffer.MemoryCopy(pSrc, pDst, bytes.Length, sizeof(Storage.Entities.Entity));
        }

        return bytes;
    }

    private unsafe void RestoreRecordDirect(byte[] snapshot)
    {
        Storage.Entities.Entity e;

        fixed (byte* pSrc = snapshot)
        {
            Buffer.MemoryCopy(pSrc, &e, sizeof(Storage.Entities.Entity), sizeof(Storage.Entities.Entity));
        }

        if (_index.TrySearch(e.Id, out int existingGi))
        {
            var existing = _store.Read(existingGi);
            if (!existing.IsDeleted)
                return;
        }

        EntityFlags.ClearFlag(ref e.Flags, EntityFlags.IsDeleted);
        EntityFlags.SetFlag(ref e.Flags, EntityFlags.IsActive);
        EntityFlags.SetFlag(ref e.Flags, EntityFlags.IsIndexed);
        e.UpdateChecksum();

        int globalIndex = _store.Append(e);
        _index.Insert(e.Id, globalIndex);
        Interlocked.Increment(ref _activeCount);

        if (e.Id > _nextId)
            Interlocked.Exchange(ref _nextId, e.Id);
    }

    public TransactionEntry[] GetTransactionLog() => _log.GetAllDescending();

    public void ClearTransactionLog() => _log.Clear();

    public (long Timestamp, int Count)[] GetTimeSeries(int maxBuckets = 12)
    {
        if (_store.TotalCount == 0)
            return [];

        long minTs = long.MaxValue;
        long maxTs = long.MinValue;

        _store.ForEach((_, e) =>
        {
            if (e.IsDeleted) return;
            if (e.Timestamp < minTs) minTs = e.Timestamp;
            if (e.Timestamp > maxTs) maxTs = e.Timestamp;
        });

        if (minTs == long.MaxValue)
            return [];

        long range = maxTs - minTs;
        int buckets = range == 0 ? 1 : maxBuckets;
        long bucketSize = range == 0 ? 1 : (range / buckets) + 1;
        var counts = new int[buckets];

        _store.ForEach((_, e) =>
        {
            if (e.IsDeleted) return;
            int bi = (int)Math.Min((e.Timestamp - minTs) / bucketSize, buckets - 1);
            counts[bi]++;
        });

        var result = new (long Timestamp, int Count)[buckets];
        for (int i = 0; i < buckets; i++)
            result[i] = (minTs + i * bucketSize, counts[i]);

        return result;
    }

    private static SearchResult EntityToResult(in Storage.Entities.Entity e, int gi)
    {
        return new SearchResult(e.Id, e.GetKey(), e.GetData(), e.Timestamp, e.Flags, gi);
    }

    public void Dispose()
    {
        _store.Dispose();
        _index.Dispose();
    }
}

internal sealed class DynamicArray<T>
{
    private T[] _items;
    private int _count;

    public DynamicArray(int capacity) => _items = new T[capacity];

    public int Count => _count;

    public void Add(T item)
    {
        if (_count == _items.Length)
        {
            var next = new T[_items.Length * 2];
            _items.AsSpan(0, _count).CopyTo(next);
            _items = next;
        }
        _items[_count++] = item;
    }

    public T[] ToArray()
    {
        var result = new T[_count];
        _items.AsSpan(0, _count).CopyTo(result);
        return result;
    }
}
