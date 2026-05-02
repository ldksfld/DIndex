using DIndex.Core.Application.Interfaces;

namespace DIndex.Core.Application.Services;

public static class CsvImporter
{
    public static async Task<(int imported, int skipped, bool cancelled)> ImportAsync(
        IDataEngine engine,
        string filePath,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var lines = File.ReadAllLines(filePath);
            var records = new CsvRecord[lines.Length];
            int valid = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                if (ct.IsCancellationRequested)
                    return (0, 0, true);

                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var parts = lines[i].Split(';');

                if (parts.Length < 3)
                    continue;

                if (!long.TryParse(parts[0].Trim(), out long id))
                    continue;

                records[valid++] = new CsvRecord(id, parts[1].Trim(), parts[2].Trim());
            }

            var sortSpan = records.AsSpan(0, valid);
            Search.QuickSorter.Sort(sortSpan, (a, b) => a.Id.CompareTo(b.Id));

            int imported = 0;
            int skipped = 0;
            int batchSize = Math.Max(1, valid / 100);

            for (int i = 0; i < valid; i++)
            {
                if (ct.IsCancellationRequested)
                    return (imported, skipped, true);

                var r = records[i];

                try
                {
                    engine.AddRecord(r.Key, r.Data, r.Id);
                    imported++;
                }
                catch
                {
                    skipped++;
                }

                if ((i + 1) % batchSize == 0)
                    progress?.Report((int)((long)(i + 1) * 100 / valid));
            }

            progress?.Report(100);
            return (imported, skipped, false);
        });
    }

    public static string[] Preview(string filePath, int maxLines = 10)
    {
        var lines = new string[maxLines];

        using var reader = new StreamReader(filePath);

        for (int i = 0; i < maxLines; i++)
        {
            string? line = reader.ReadLine();

            if (line is null)
                break;

            lines[i] = line;
        }

        return lines;
    }

    private readonly record struct CsvRecord(long Id, string Key, string Data);
}
