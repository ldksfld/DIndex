using System.Runtime.CompilerServices;

namespace DIndex.Core.Indexing.Bst;

public sealed class BinarySearchTree : IDisposable
{
    public const int DepthWarningThreshold = 5000;

    private BstNode? _root;
    private int _count;
    private long _minKey = long.MaxValue;
    private long _maxKey = long.MinValue;
    private readonly ReaderWriterLockSlim _lock = new();

    public int Count => _count;
    public long MinKey => _minKey;
    public long MaxKey => _maxKey;

    public bool IsDeep
    {
        get
        {
            int h = Height();
            return h > DepthWarningThreshold;
        }
    }

    public void Insert(long key, int storeIndex)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_root is null)
            {
                _root = new BstNode(key, storeIndex);
                _count = 1;
                _minKey = key;
                _maxKey = key;
                return;
            }

            BstNode current = _root;
            while (true)
            {
                if (key < current.Key)
                {
                    if (current.Left is null)
                    {
                        current.Left = new BstNode(key, storeIndex);
                        break;
                    }
                    current = current.Left;
                }
                else if (key > current.Key)
                {
                    if (current.Right is null)
                    {
                        current.Right = new BstNode(key, storeIndex);
                        break;
                    }
                    current = current.Right;
                }
                else
                {
                    current.StoreIndex = storeIndex;
                    return;
                }
            }

            _count++;
            if (key < _minKey) _minKey = key;
            if (key > _maxKey) _maxKey = key;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool TrySearch(long key, out int storeIndex)
    {
        _lock.EnterReadLock();
        try
        {
            BstNode? current = _root;
            while (current is not null)
            {
                if (key == current.Key)
                {
                    storeIndex = current.StoreIndex;
                    return true;
                }

                current = key < current.Key ? current.Left : current.Right;
            }

            storeIndex = -1;
            return false;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool Delete(long key)
    {
        _lock.EnterWriteLock();
        try
        {
            BstNode? parent = null;
            BstNode? current = _root;

            while (current is not null && current.Key != key)
            {
                parent = current;
                current = key < current.Key ? current.Left : current.Right;
            }

            if (current is null)
                return false;

            BstNode? replacement;

            if (current.Left is null)
            {
                replacement = current.Right;
            }
            else if (current.Right is null)
            {
                replacement = current.Left;
            }
            else
            {
                BstNode succParent = current;
                BstNode succ = current.Right;
                while (succ.Left is not null)
                {
                    succParent = succ;
                    succ = succ.Left;
                }

                if (succParent != current)
                {
                    succParent.Left = succ.Right;
                    succ.Right = current.Right;
                }
                succ.Left = current.Left;
                replacement = succ;
            }

            if (parent is null)
            {
                _root = replacement;
            }
            else if (parent.Left == current)
            {
                parent.Left = replacement;
            }
            else
            {
                parent.Right = replacement;
            }

            _count--;

            if (_count == 0)
            {
                _minKey = long.MaxValue;
                _maxKey = long.MinValue;
            }
            else if (key == _minKey || key == _maxKey)
            {
                BstNode cur = _root!;
                while (cur.Left is not null) cur = cur.Left;
                _minKey = cur.Key;

                cur = _root!;
                while (cur.Right is not null) cur = cur.Right;
                _maxKey = cur.Key;
            }

            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void InOrderTraversal(Action<long, int> visitor)
    {
        _lock.EnterReadLock();
        try
        {
            try
            {
                InOrderRec(_root, visitor);
            }
            catch (InsufficientExecutionStackException)
            {
                throw new InvalidOperationException(
                    "Глибина індексу перевищує безпечний поріг для рекурсивного обходу. " +
                    "Перебудуйте індекс випадковим порядком ключів або очистіть зайві записи.");
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static void InOrderRec(BstNode? node, Action<long, int> visitor)
    {
        if (node is null) return;
        RuntimeHelpers.EnsureSufficientExecutionStack();
        InOrderRec(node.Left, visitor);
        visitor(node.Key, node.StoreIndex);
        InOrderRec(node.Right, visitor);
    }

    public void PreOrderTraversal(Action<long, int> visitor)
    {
        _lock.EnterReadLock();
        try
        {
            try
            {
                PreOrderRec(_root, visitor);
            }
            catch (InsufficientExecutionStackException)
            {
                throw new InvalidOperationException(
                    "Глибина індексу перевищує безпечний поріг для pre-order обходу.");
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static void PreOrderRec(BstNode? node, Action<long, int> visitor)
    {
        if (node is null) return;
        RuntimeHelpers.EnsureSufficientExecutionStack();
        visitor(node.Key, node.StoreIndex);
        PreOrderRec(node.Left, visitor);
        PreOrderRec(node.Right, visitor);
    }

    public void PostOrderTraversal(Action<long, int> visitor)
    {
        _lock.EnterReadLock();
        try
        {
            try
            {
                PostOrderRec(_root, visitor);
            }
            catch (InsufficientExecutionStackException)
            {
                throw new InvalidOperationException(
                    "Глибина індексу перевищує безпечний поріг для post-order обходу.");
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static void PostOrderRec(BstNode? node, Action<long, int> visitor)
    {
        if (node is null) return;
        RuntimeHelpers.EnsureSufficientExecutionStack();
        PostOrderRec(node.Left, visitor);
        PostOrderRec(node.Right, visitor);
        visitor(node.Key, node.StoreIndex);
    }

    public void RangeSearch(long minKey, long maxKey, Action<long, int> visitor)
    {
        _lock.EnterReadLock();
        try
        {
            try
            {
                RangeSearchRec(_root, minKey, maxKey, visitor);
            }
            catch (InsufficientExecutionStackException)
            {
                throw new InvalidOperationException(
                    "Глибина індексу перевищує безпечний поріг для пошуку діапазоном.");
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static void RangeSearchRec(BstNode? node, long min, long max, Action<long, int> visitor)
    {
        if (node is null) return;
        RuntimeHelpers.EnsureSufficientExecutionStack();

        if (node.Key > min)
            RangeSearchRec(node.Left, min, max, visitor);

        if (node.Key >= min && node.Key <= max)
            visitor(node.Key, node.StoreIndex);

        if (node.Key < max)
            RangeSearchRec(node.Right, min, max, visitor);
    }

    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            try
            {
                ClearRec(_root);
            }
            catch (InsufficientExecutionStackException)
            {
            }

            _root = null;
            _count = 0;
            _minKey = long.MaxValue;
            _maxKey = long.MinValue;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private static void ClearRec(BstNode? node)
    {
        if (node is null) return;
        RuntimeHelpers.EnsureSufficientExecutionStack();
        ClearRec(node.Left);
        ClearRec(node.Right);
        node.Left = null;
        node.Right = null;
    }

    public int Height()
    {
        _lock.EnterReadLock();
        try
        {
            try
            {
                return HeightRec(_root);
            }
            catch (InsufficientExecutionStackException)
            {
                return int.MaxValue;
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static int HeightRec(BstNode? node)
    {
        if (node is null) return 0;
        RuntimeHelpers.EnsureSufficientExecutionStack();
        return 1 + Math.Max(HeightRec(node.Left), HeightRec(node.Right));
    }

    public void Dispose() => _lock.Dispose();
}
