using System.Threading;
using DIndex.Core.Storage.Entities;

namespace DIndex.Core.Storage;

public sealed class PagedMemoryStore : IDataStore, IDisposable
{
    private const int MaxPages = 256;

    private readonly MemoryPage[] _pages;
    private int _pageCount;
    private int _totalCount;
    private readonly ReaderWriterLockSlim _lock = new();

    public PagedMemoryStore()
    {
        _pages = new MemoryPage[MaxPages];
        _pages[0] = new MemoryPage();
        _pageCount = 1;
    }

    public int TotalCount => _totalCount;
    public int PageCount => _pageCount;

    public int Append(in Entity entity)
    {
        _lock.EnterWriteLock();
        try
        {
            var page = _pages[_pageCount - 1];

            if (page.IsFull)
            {
                if (_pageCount >= MaxPages)
                    throw new InvalidOperationException($"Досягнуто максимум {MaxPages} сторінок пам'яті.");

                page = new MemoryPage();
                _pages[_pageCount++] = page;
            }

            page.TryWrite(entity, out int local);
            int global = (_pageCount - 1) * MemoryPage.PageCapacity + local;
            _totalCount++;

            return global;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Entity Read(int globalIndex)
    {
        _lock.EnterReadLock();
        try
        {
            DecomposeIndex(globalIndex, out int p, out int l);
            return _pages[p].Read(l);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Update(int globalIndex, in Entity entity)
    {
        _lock.EnterWriteLock();
        try
        {
            DecomposeIndex(globalIndex, out int p, out int l);
            _pages[p].Update(l, entity);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void ForEach(Action<int, Entity> action)
    {
        _lock.EnterReadLock();
        try
        {
            for (int p = 0; p < _pageCount; p++)
            {
                var page = _pages[p];
                int count = page.Count;

                for (int l = 0; l < count; l++)
                {
                    int global = p * MemoryPage.PageCapacity + l;
                    action(global, page.Read(l));
                }
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public (MemoryPage page, int count)[] GetPages()
    {
        _lock.EnterReadLock();
        try
        {
            var result = new (MemoryPage, int)[_pageCount];

            for (int i = 0; i < _pageCount; i++)
                result[i] = (_pages[i], _pages[i].Count);

            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Reset()
    {
        _lock.EnterWriteLock();
        try
        {
            for (int i = 0; i < _pageCount; i++)
                _pages[i] = null!;

            _pages[0] = new MemoryPage();
            _pageCount = 1;
            _totalCount = 0;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public int AppendDirect(in Entity entity)
    {
        var page = _pages[_pageCount - 1];

        if (page.IsFull)
        {
            if (_pageCount >= MaxPages)
                throw new InvalidOperationException("Досягнуто максимум сторінок при завантаженні.");

            page = new MemoryPage();
            _pages[_pageCount++] = page;
        }

        page.TryWrite(entity, out int local);
        int global = (_pageCount - 1) * MemoryPage.PageCapacity + local;
        _totalCount++;

        return global;
    }

    private void DecomposeIndex(int globalIndex, out int pageIndex, out int localIndex)
    {
        if ((uint)globalIndex >= (uint)_totalCount)
            throw new IndexOutOfRangeException($"Глобальний індекс {globalIndex} виходить за межі [0, {_totalCount})");

        pageIndex = globalIndex / MemoryPage.PageCapacity;
        localIndex = globalIndex % MemoryPage.PageCapacity;
    }

    public void Dispose() => _lock.Dispose();
}
