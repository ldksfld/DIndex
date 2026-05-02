namespace DIndex.Core.Search;

public static class InterpolationSearcher
{
    public static int Search(ReadOnlySpan<long> sortedKeys, long target)
    {
        int low = 0;
        int high = sortedKeys.Length - 1;

        while (low <= high && target >= sortedKeys[low] && target <= sortedKeys[high])
        {
            long range = sortedKeys[high] - sortedKeys[low];

            if (range == 0)
                return sortedKeys[low] == target ? low : -1;

            int pos = low + (int)(((long)(target - sortedKeys[low]) * (high - low)) / range);

            if (pos < low) pos = low;
            if (pos > high) pos = high;

            if (sortedKeys[pos] == target)
                return pos;
            else if (sortedKeys[pos] < target)
                low = pos + 1;
            else
                high = pos - 1;
        }

        return -1;
    }

    public static int LowerBound(ReadOnlySpan<long> sortedKeys, long target)
    {
        int lo = 0;
        int hi = sortedKeys.Length;

        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;

            if (sortedKeys[mid] < target)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }
}
