namespace DIndex.Core.Indexing.LinkedList;

public sealed class TransactionLog
{
    private const int MaxCapacity = 100;

    private readonly object _lock = new();
    private readonly DoublyLinkedList<TransactionEntry> _log = new();
    private readonly DoublyLinkedList<TransactionEntry> _redoStack = new();

    public int Count
    {
        get
        {
            lock (_lock)
                return _log.Count;
        }
    }

    public bool CanUndo
    {
        get
        {
            lock (_lock)
                return _log.Count > 0;
        }
    }

    public bool CanRedo
    {
        get
        {
            lock (_lock)
                return _redoStack.Count > 0;
        }
    }

    public void Record(TransactionEntry entry)
    {
        lock (_lock)
        {
            if (_log.Count >= MaxCapacity)
                _log.RemoveFirst(out _);

            _log.AddLast(entry);
            _redoStack.Clear();
        }
    }

    public TransactionEntry? PeekLast()
    {
        lock (_lock)
        {
            return _log.Tail?.Value;
        }
    }

    public TransactionEntry? Undo(byte[]? snapshotForRedo = null)
    {
        lock (_lock)
        {
            if (!_log.RemoveLast(out var entry))
                return null;

            var forRedo = snapshotForRedo is not null
                ? entry with { EntitySnapshot = snapshotForRedo }
                : entry;

            _redoStack.AddLast(forRedo);
            return entry;
        }
    }

    public TransactionEntry? Redo()
    {
        lock (_lock)
        {
            if (!_redoStack.RemoveLast(out var entry))
                return null;

            if (_log.Count >= MaxCapacity)
                _log.RemoveFirst(out _);

            _log.AddLast(entry);
            return entry;
        }
    }

    public TransactionEntry[] GetAllDescending()
    {
        lock (_lock)
        {
            return _log.ToArrayReversed();
        }
    }

    public TransactionEntry[] GetAll()
    {
        lock (_lock)
        {
            return _log.ToArray();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _log.Clear();
            _redoStack.Clear();
        }
    }
}
