namespace DIndex.Core.Search;

public static class QuickSorter
{
    private const int InsertionSortThreshold = 16;

    public static void Sort<T>(Span<T> data, Comparison<T> comparer)
    {
        if (data.Length <= 1)
            return;

        QuickSortInternal(data, 0, data.Length - 1, comparer);
    }

    private static void QuickSortInternal<T>(Span<T> data, int low, int high, Comparison<T> comparer)
    {
        while (low < high)
        {
            if (high - low <= InsertionSortThreshold)
            {
                InsertionSort(data, low, high, comparer);
                return;
            }

            int pivot = Partition(data, low, high, comparer);

            if (pivot - low < high - pivot)
            {
                QuickSortInternal(data, low, pivot - 1, comparer);
                low = pivot + 1;
            }
            else
            {
                QuickSortInternal(data, pivot + 1, high, comparer);
                high = pivot - 1;
            }
        }
    }

    private static int Partition<T>(Span<T> data, int low, int high, Comparison<T> comparer)
    {
        int mid = low + (high - low) / 2;
        SortThree(data, low, mid, high, comparer);
        Swap(ref data[mid], ref data[high - 1]);
        T pivot = data[high - 1];

        int i = low;
        int j = high - 1;

        while (true)
        {
            while (comparer(data[++i], pivot) < 0) { }
            while (comparer(data[--j], pivot) > 0) { }

            if (i >= j)
                break;

            Swap(ref data[i], ref data[j]);
        }

        Swap(ref data[i], ref data[high - 1]);
        return i;
    }

    private static void SortThree<T>(Span<T> data, int a, int b, int c, Comparison<T> comparer)
    {
        if (comparer(data[a], data[b]) > 0) Swap(ref data[a], ref data[b]);
        if (comparer(data[b], data[c]) > 0) Swap(ref data[b], ref data[c]);
        if (comparer(data[a], data[b]) > 0) Swap(ref data[a], ref data[b]);
    }

    private static void InsertionSort<T>(Span<T> data, int low, int high, Comparison<T> comparer)
    {
        for (int i = low + 1; i <= high; i++)
        {
            T key = data[i];
            int j = i - 1;

            while (j >= low && comparer(data[j], key) > 0)
            {
                data[j + 1] = data[j];
                j--;
            }

            data[j + 1] = key;
        }
    }

    private static void Swap<T>(ref T a, ref T b)
    {
        T t = a;
        a = b;
        b = t;
    }
}
