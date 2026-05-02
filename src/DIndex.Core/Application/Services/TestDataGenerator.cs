using DIndex.Core.Application.Interfaces;

namespace DIndex.Core.Application.Services;

public static class TestDataGenerator
{
    private static readonly string[] Domains =
    [
        "gmail.com", "yahoo.com", "ukr.net", "meta.ua", "outlook.com"
    ];

    private static readonly string[] Prefixes =
    [
        "user", "admin", "test", "demo", "client", "srv", "host", "node"
    ];

    public static async Task<(int generated, bool cancelled)> GenerateAsync(
        IDataEngine engine,
        int count,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var rng = new Random(42);
            int batchSize = Math.Max(1, count / 100);
            int generated = 0;

            for (int i = 0; i < count; i++)
            {
                if (ct.IsCancellationRequested)
                    return (generated, true);

                string key = BuildEmail(rng);
                string data = $"record-{i:D8}-payload-{rng.Next(100_000, 999_999)}";

                engine.AddRecord(key, data);
                generated++;

                if ((i + 1) % batchSize == 0)
                    progress?.Report((int)((long)(i + 1) * 100 / count));
            }

            progress?.Report(100);
            return (generated, false);
        });
    }

    private static string BuildEmail(Random rng)
    {
        string prefix = Prefixes[rng.Next(Prefixes.Length)];
        string domain = Domains[rng.Next(Domains.Length)];
        int suffix = rng.Next(1000, 99999);

        return $"{prefix}{suffix}@{domain}";
    }
}
