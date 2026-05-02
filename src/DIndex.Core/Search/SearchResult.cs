namespace DIndex.Core.Search;

public sealed record SearchResult(
    long Id,
    string Key,
    string Data,
    long Timestamp,
    byte Flags,
    int GlobalIndex);
