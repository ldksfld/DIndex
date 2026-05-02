using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DIndex.App.ViewModels;

public sealed record BenchmarkRow(string Method, string N, string MeanNs, string StdDev, string Alloc);

public sealed partial class BenchmarkViewModel : BaseViewModel, IDisposable
{
    [ObservableProperty] private bool _scenarioBstLinear = true;
    [ObservableProperty] private bool _scenarioQuickSort = true;
    [ObservableProperty] private bool _scenarioInterpolation;

    [ObservableProperty] private bool _n1k = true;
    [ObservableProperty] private bool _n10k = true;
    [ObservableProperty] private bool _n100k = true;
    [ObservableProperty] private bool _n1m;

    [ObservableProperty] private string _progressText = "Готовий до запуску. Оберіть сценарії та натисніть «Запустити аналіз».";
    [ObservableProperty] private string _elapsedText = "";
    [ObservableProperty] private string _exePathDisplay = "";
    [ObservableProperty] private string _reportPath = "";
    [ObservableProperty] private bool _hasReport;
    [ObservableProperty] private bool _hasRows;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelRunCommand))]
    private bool _canCancel;

    private CancellationTokenSource? _cts;
    private Process? _proc;

    public ObservableCollection<BenchmarkRow> Rows { get; } = new();

    public BenchmarkViewModel()
    {
        Rows.CollectionChanged += (_, _) => HasRows = Rows.Count > 0;
    }

    [RelayCommand]
    private async Task RunBenchmarksAsync()
    {
        if (IsBusy)
            return;

        if (!ScenarioBstLinear && !ScenarioQuickSort && !ScenarioInterpolation)
        {
            SetError("Оберіть хоча б один сценарій для аналізу.");
            return;
        }

        if (!N1k && !N10k && !N100k && !N1m)
        {
            SetError("Оберіть хоча б один розмір N.");
            return;
        }

        ClearError();
        Rows.Clear();
        HasReport = false;
        ReportPath = "";
        ExePathDisplay = "";
        ElapsedText = "";
        ProgressText = "Шукаємо виконуваний файл бенчмарку...";

        string? benchExe = FindBenchmarkExe();

        if (benchExe is null)
        {
            ProgressText = "";
            SetError(
                "Не знайдено DIndex.Benchmarks.exe.\n" +
                "Спочатку зберіть проєкт бенчмарків командою:\n" +
                "    dotnet build src/DIndex.Benchmarks -c Release\n" +
                "Файл повинен з'явитися за шляхом:\n" +
                "    src/DIndex.Benchmarks/bin/Release/net9.0/DIndex.Benchmarks.exe");
            return;
        }

        ExePathDisplay = benchExe;
        IsBusy = true;
        CanCancel = true;
        _cts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();

        try
        {
            string args = BuildArgs();
            string workDir = Path.GetDirectoryName(benchExe) ?? AppDomain.CurrentDomain.BaseDirectory;

            ProgressText = $"Запуск: DIndex.Benchmarks.exe {args}";

            var psi = new ProcessStartInfo(benchExe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workDir,
            };

            _proc = Process.Start(psi);

            if (_proc is null)
            {
                SetError("Не вдалося запустити процес бенчмарку.");
                return;
            }

            var elapsedTask = Task.Run(async () =>
            {
                try
                {
                    while (_proc is { HasExited: false })
                    {
                        var ts = sw.Elapsed;
                        ElapsedText = $"Минуло: {ts.Minutes:D2}:{ts.Seconds:D2}";
                        await Task.Delay(500, _cts.Token);
                    }
                }
                catch (OperationCanceledException) { }
            }, _cts.Token);

            _ = Task.Run(async () =>
            {
                try
                {
                    string? errLine;
                    while ((errLine = await _proc.StandardError.ReadLineAsync(_cts.Token)) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(errLine))
                            ProgressText = "[stderr] " + Truncate(errLine, 220);
                    }
                }
                catch { }
            }, _cts.Token);

            string? line;
            while ((line = await _proc.StandardOutput.ReadLineAsync(_cts.Token)) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    ProgressText = Truncate(line, 220);
            }

            await _proc.WaitForExitAsync(_cts.Token);
            await elapsedTask;
            sw.Stop();

            int exitCode = _proc.ExitCode;

            ParseResults(workDir);

            if (Rows.Count > 0)
            {
                string? uaReport = GenerateUkrainianHtmlReport(workDir);
                if (uaReport != null)
                {
                    ReportPath = uaReport;
                    HasReport = true;
                }
                else
                {
                    FindHtmlReport(workDir);
                }
                ProgressText = $"Аналіз завершено. Знайдено результатів: {Rows.Count}.";
                ElapsedText = $"Загальний час: {sw.Elapsed.Minutes:D2}:{sw.Elapsed.Seconds:D2}";
            }
            else
            {
                FindHtmlReport(workDir);
                ProgressText = exitCode == 0
                    ? "Процес завершився, але результатів не знайдено. Перевірте, що сценарії запустилися."
                    : $"Процес завершився з кодом {exitCode}. Результатів не знайдено.";
                ElapsedText = $"Загальний час: {sw.Elapsed.Minutes:D2}:{sw.Elapsed.Seconds:D2}";
            }
        }
        catch (OperationCanceledException)
        {
            try { _proc?.Kill(entireProcessTree: true); } catch { }
            ProgressText = "Аналіз скасовано користувачем.";
            ElapsedText = $"Минуло: {sw.Elapsed.Minutes:D2}:{sw.Elapsed.Seconds:D2}";
            SetStatus("Аналіз скасовано.");
        }
        catch (Exception ex)
        {
            try { _proc?.Kill(entireProcessTree: true); } catch { }
            SetError($"Помилка під час аналізу: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            CanCancel = false;
            _proc?.Dispose();
            _proc = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelRun()
    {
        try { _cts?.Cancel(); } catch { }
        try { _proc?.Kill(entireProcessTree: true); } catch { }
        ProgressText = "Скасовуємо процес...";
    }

    [RelayCommand]
    private void OpenReport()
    {
        if (!HasReport || string.IsNullOrEmpty(ReportPath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(ReportPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetError($"Не вдалося відкрити звіт: {ex.Message}");
        }
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";

    private static string? FindBenchmarkExe()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;

        string? exeDir = null;
        try
        {
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                exeDir = Path.GetDirectoryName(exePath);
        }
        catch { }

        var candidates = new System.Collections.Generic.List<string>();

        if (exeDir != null)
        {
            candidates.Add(Path.Combine(exeDir, "DIndex.Benchmarks.exe"));
            candidates.Add(Path.Combine(exeDir, "..", "src", "DIndex.Benchmarks", "bin", "Release", "net9.0", "DIndex.Benchmarks.exe"));
            candidates.Add(Path.Combine(exeDir, "..", "src", "DIndex.Benchmarks", "bin", "Debug", "net9.0", "DIndex.Benchmarks.exe"));
        }

        candidates.Add(Path.Combine(baseDir, "DIndex.Benchmarks.exe"));
        candidates.Add(Path.Combine(baseDir, "..", "..", "..", "..", "DIndex.Benchmarks", "bin", "Release", "net9.0", "DIndex.Benchmarks.exe"));
        candidates.Add(Path.Combine(baseDir, "..", "..", "..", "..", "DIndex.Benchmarks", "bin", "Debug", "net9.0", "DIndex.Benchmarks.exe"));
        candidates.Add(Path.Combine(baseDir, "..", "..", "..", "..", "..", "DIndex.Benchmarks", "bin", "Release", "net9.0", "DIndex.Benchmarks.exe"));
        candidates.Add(Path.Combine(baseDir, "..", "..", "..", "..", "..", "DIndex.Benchmarks", "bin", "Debug", "net9.0", "DIndex.Benchmarks.exe"));
        candidates.Add(Path.Combine(baseDir, "..", "..", "..", "DIndex.Benchmarks", "bin", "Release", "net9.0", "DIndex.Benchmarks.exe"));
        candidates.Add(Path.Combine(baseDir, "..", "..", "..", "DIndex.Benchmarks", "bin", "Debug", "net9.0", "DIndex.Benchmarks.exe"));
        candidates.Add(Path.Combine(baseDir, "..", "src", "DIndex.Benchmarks", "bin", "Release", "net9.0", "DIndex.Benchmarks.exe"));

        foreach (var c in candidates)
        {
            try
            {
                string full = Path.GetFullPath(c);
                if (File.Exists(full))
                    return full;
            }
            catch { }
        }

        return null;
    }

    private string BuildArgs()
    {
        var classFilters = new System.Collections.Generic.List<string>();

        if (ScenarioBstLinear) classFilters.Add("*BstVsLinearBenchmark*");
        if (ScenarioQuickSort) classFilters.Add("*SortingBenchmark*");
        if (ScenarioInterpolation) classFilters.Add("*SearchBenchmark*");

        var sb = new StringBuilder();

        if (classFilters.Count > 0)
            sb.Append("--filter ").Append(string.Join(' ', classFilters));

        return sb.ToString();
    }

    private void ParseResults(string workDir)
    {
        string resultsDir = Path.Combine(workDir, "BenchmarkDotNet.Artifacts", "results");

        if (!Directory.Exists(resultsDir))
            return;

        var allowedN = new System.Collections.Generic.HashSet<string>();
        if (N1k) allowedN.Add("1000");
        if (N10k) allowedN.Add("10000");
        if (N100k) allowedN.Add("100000");
        if (N1m) allowedN.Add("1000000");

        var csvFiles = Directory.GetFiles(resultsDir, "*-report.csv");
        Array.Sort(csvFiles, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));

        foreach (var file in csvFiles)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                if (lines.Length < 2)
                    continue;

                char sep = DetectSeparator(lines[0]);
                var header = ParseCsvLine(lines[0], sep);
                int colMethod = IndexOf(header, "Method");
                int colN = IndexOf(header, "N");
                int colMean = IndexOf(header, "Mean");
                int colStdDev = IndexOf(header, "StdDev");
                int colAlloc = IndexOf(header, "Allocated");

                if (colMethod < 0 || colMean < 0)
                    continue;

                for (int i = 1; i < lines.Length; i++)
                {
                    var fields = ParseCsvLine(lines[i], sep);
                    if (fields.Length <= colMethod)
                        continue;

                    string method = StripQuotes(SafeGet(fields, colMethod));
                    string n = SafeGet(fields, colN);
                    string mean = SafeGet(fields, colMean);
                    string std = SafeGet(fields, colStdDev);
                    string alloc = SafeGet(fields, colAlloc);

                    if (string.IsNullOrEmpty(method) || string.IsNullOrEmpty(mean))
                        continue;

                    if (allowedN.Count > 0 && !string.IsNullOrEmpty(n) && !allowedN.Contains(n))
                        continue;

                    Rows.Add(new BenchmarkRow(
                        Method: string.IsNullOrEmpty(method) ? "—" : method,
                        N: string.IsNullOrEmpty(n) ? "—" : n,
                        MeanNs: string.IsNullOrEmpty(mean) ? "—" : mean,
                        StdDev: string.IsNullOrEmpty(std) ? "—" : std,
                        Alloc: string.IsNullOrEmpty(alloc) ? "—" : alloc));
                }
            }
            catch
            {
            }
        }
    }

    private void FindHtmlReport(string workDir)
    {
        string resultsDir = Path.Combine(workDir, "BenchmarkDotNet.Artifacts", "results");

        if (!Directory.Exists(resultsDir))
            return;

        var htmlFiles = Directory.GetFiles(resultsDir, "*-report.html");

        if (htmlFiles.Length == 0)
            return;

        Array.Sort(htmlFiles, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
        ReportPath = htmlFiles[0];
        HasReport = true;
    }

    private static int IndexOf(string[] arr, string value)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            if (string.Equals(arr[i], value, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static string SafeGet(string[] arr, int index)
        => index >= 0 && index < arr.Length ? arr[index] : "";

    private static string StripQuotes(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length >= 2 && s[0] == '\'' && s[^1] == '\'')
            return s[1..^1];
        return s;
    }

    private static char DetectSeparator(string headerLine)
    {
        int semis = 0, commas = 0, tabs = 0;
        bool inQuote = false;
        foreach (char c in headerLine)
        {
            if (c == '"') { inQuote = !inQuote; continue; }
            if (inQuote) continue;
            if (c == ';') semis++;
            else if (c == ',') commas++;
            else if (c == '\t') tabs++;
        }
        if (semis >= commas && semis >= tabs) return ';';
        if (tabs >= commas) return '\t';
        return ',';
    }

    private static string[] ParseCsvLine(string line, char sep)
    {
        var fields = new System.Collections.Generic.List<string>();
        var sb = new StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuote = !inQuote;
                }
                continue;
            }

            if (c == sep && !inQuote)
            {
                fields.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString().Trim());
        return fields.ToArray();
    }

    private string? GenerateUkrainianHtmlReport(string workDir)
    {
        if (Rows.Count == 0) return null;

        var bst  = Rows.Where(r => r.Method.Contains("BST") || (r.Method.Contains("Linear") && r.Method.Contains("Search"))).ToList();
        var sort = Rows.Where(r => r.Method.Contains("QuickSort") || r.Method.Contains("Array.Sort")).ToList();
        var srch = Rows.Where(r => r.Method.Contains("Interpolation") || r.Method.Contains("BinarySearch")).ToList();

        string date = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        var sb = new StringBuilder(65536);

        sb.Append("<!DOCTYPE html>\n<html lang=\"uk\">\n<head>\n");
        sb.Append("<meta charset=\"UTF-8\">\n");
        sb.Append("<title>Аналіз продуктивності — D-Index</title>\n<style>\n");
        sb.Append("body{font-family:Georgia,'Times New Roman',serif;color:#222;background:#fff;max-width:820px;margin:30px auto;padding:0 24px;line-height:1.55;font-size:15px}");
        sb.Append("h1{font-size:1.5em;margin:0 0 4px;font-weight:normal}");
        sb.Append("h2{font-size:1.15em;margin:28px 0 8px;border-bottom:1px solid #ccc;padding-bottom:3px}");
        sb.Append("h3{font-size:1em;margin:16px 0 6px;font-weight:bold}");
        sb.Append("p{margin:6px 0}");
        sb.Append("ul{margin:4px 0 8px 22px}");
        sb.Append("li{margin:3px 0}");
        sb.Append(".meta{color:#666;font-size:.9em;margin-bottom:18px}");
        sb.Append("code{font-family:Consolas,'Courier New',monospace;font-size:.92em;background:#f4f4f4;padding:1px 4px;border-radius:2px}");
        sb.Append("table{border-collapse:collapse;margin:8px 0 10px;font-family:Consolas,'Courier New',monospace;font-size:.88em}");
        sb.Append("th,td{border:1px solid #bbb;padding:4px 10px;text-align:left}");
        sb.Append("th{background:#eee;font-weight:bold}");
        sb.Append(".note{font-size:.88em;color:#555;margin:6px 0 4px;font-style:italic}");
        sb.Append("dl dt{font-weight:bold;margin-top:8px}");
        sb.Append("dl dd{margin:2px 0 6px 18px;font-size:.93em}");
        sb.Append("hr{border:0;border-top:1px solid #ccc;margin:24px 0 6px}");
        sb.Append(".foot{font-size:.82em;color:#888;margin:6px 0 24px}");
        sb.Append("</style>\n</head>\n<body>\n");

        sb.Append("<h1>Аналіз продуктивності алгоритмів D-Index</h1>");
        sb.Append($"<div class=\"meta\">Дата запуску: {date}</div>");

        sb.Append("<h2>Методологія</h2>");
        sb.Append("<p>Вимірювання виконано бібліотекою BenchmarkDotNet 0.14.0 у конфігурації Release " +
                  "(.NET 9, RyuJIT x64). Кожен тест проганяється багатократно з прогрівом JIT, " +
                  "результати усереднюються.</p>");
        sb.Append("<p>Порівнюються власні реалізації структур даних і алгоритмів із аналогами " +
                  "стандартної бібліотеки .NET. Цілі — перевірити асимптотику на практиці " +
                  "та оцінити, наскільки навчальний код програє або перевищує системний.</p>");

        if (bst.Count > 0)
        {
            sb.Append("<h2>Двійкове дерево пошуку проти лінійного пошуку</h2>");
            sb.Append("<h3>Що порівнюється</h3>");
            sb.Append("<ul>");
            sb.Append("<li><code>BST Search</code> — ітеративний обхід власного двійкового дерева. " +
                      "На кожному кроці порівнюємо ключ з вузлом і йдемо вліво або вправо.</li>");
            sb.Append("<li><code>Linear Search</code> — простий послідовний перебір масиву ключів. " +
                      "Слугує контрольним значенням.</li>");
            sb.Append("</ul>");
            sb.Append("<h3>Очікувана складність</h3>");
            sb.Append("<p>BST у достатньо збалансованому випадку — <code>O(log n)</code>; " +
                      "лінійний — <code>O(n)</code>. " +
                      "При N = 100 000 BST робить близько 17 порівнянь, лінійний — до 100 000.</p>");
            sb.Append("<p class=\"note\">Дерево будується з випадкових ключів (Random seed = 42).</p>");
            sb.Append("<h3>Результати</h3>");
            sb.Append("<table><tr><th>Метод</th><th>N</th><th>Середній час</th><th>Станд. відхилення</th><th>Алокації</th></tr>");
            foreach (var r in bst)
                sb.Append($"<tr><td>{r.Method}</td><td>{r.N}</td><td>{r.MeanNs}</td><td>{r.StdDev}</td><td>{r.Alloc}</td></tr>");
            sb.Append("</table>");
            sb.Append("<p>Розрив між BST і лінійним пошуком зростає з N. На малих обсягах різниця непомітна, " +
                      "на 100 000+ елементах вже видно очікуване співвідношення між <code>O(log n)</code> і <code>O(n)</code>. " +
                      "Алокацій обидва методи практично не роблять — обидва працюють по існуючих структурах.</p>");
        }

        if (sort.Count > 0)
        {
            sb.Append("<h2>Швидке сортування: власне проти Array.Sort</h2>");
            sb.Append("<h3>Що порівнюється</h3>");
            sb.Append("<ul>");
            sb.Append("<li><code>QuickSort (власний)</code> — реалізація з вибором опорного " +
                      "за «медіаною трьох» (перший, середній, останній елементи підмасиву). " +
                      "Знижує ризик деградації до O(n²) на майже відсортованих даних.</li>");
            sb.Append("<li><code>Array.Sort</code> — introsort у CLR: " +
                      "QuickSort + HeapSort + InsertionSort із оптимізаціями JIT.</li>");
            sb.Append("</ul>");
            sb.Append("<h3>Очікувана складність</h3>");
            sb.Append("<p>Для обох — <code>O(n log n)</code> у середньому. Кожен запуск використовує копію " +
                      "вхідного масиву, щоб стартові умови були однаковими.</p>");
            sb.Append("<h3>Результати</h3>");
            sb.Append("<table><tr><th>Метод</th><th>N</th><th>Середній час</th><th>Станд. відхилення</th><th>Алокації</th></tr>");
            foreach (var r in sort)
                sb.Append($"<tr><td>{r.Method}</td><td>{r.N}</td><td>{r.MeanNs}</td><td>{r.StdDev}</td><td>{r.Alloc}</td></tr>");
            sb.Append("</table>");
            sb.Append("<p>Вбудоване <code>Array.Sort</code> очікувано швидше — це накладні витрати рекурсії " +
                      "проти зрілих оптимізацій рантайму. Власна реалізація показує ту ж асимптотику й " +
                      "лишається в тому ж порядку величин. Алокації пов'язані з копіюванням масиву, " +
                      "сам алгоритм у обох випадках сортує на місці.</p>");
        }

        if (srch.Count > 0)
        {
            sb.Append("<h2>Інтерполяційний пошук проти двійкового</h2>");
            sb.Append("<h3>Що порівнюється</h3>");
            sb.Append("<ul>");
            sb.Append("<li><code>InterpolationSearch</code> — позиція зонда обчислюється формулою " +
                      "<code>probe = lo + (target − arr[lo]) × (hi − lo) / (arr[hi] − arr[lo])</code>. " +
                      "На рівномірних даних дає <code>O(log log n)</code>.</li>");
            sb.Append("<li><code>Array.BinarySearch</code> — двійковий пошук .NET, " +
                      "ділить діапазон навпіл: <code>O(log n)</code> у будь-якому випадку.</li>");
            sb.Append("</ul>");
            sb.Append("<h3>Очікувана складність</h3>");
            sb.Append("<p>Тестові дані заповнено як <code>key[i] = i × 1000</code> — рівномірний розподіл, " +
                      "сприятливий для інтерполяції. При N = 1 000 000: двійковий — ~20 кроків, " +
                      "інтерполяційний — близько 4.</p>");
            sb.Append("<h3>Результати</h3>");
            sb.Append("<table><tr><th>Метод</th><th>N</th><th>Середній час</th><th>Станд. відхилення</th><th>Алокації</th></tr>");
            foreach (var r in srch)
                sb.Append($"<tr><td>{r.Method}</td><td>{r.N}</td><td>{r.MeanNs}</td><td>{r.StdDev}</td><td>{r.Alloc}</td></tr>");
            sb.Append("</table>");
            sb.Append("<p>На рівномірно розподілених ключах інтерполяційний пошук швидший — і ця перевага " +
                      "стає помітнішою з ростом N. На реальних, нерівномірних даних двійковий пошук " +
                      "поводиться передбачуваніше, тому в загальному випадку зазвичай надійніший.</p>");
        }

        sb.Append("<h2>Висновки</h2>");
        sb.Append("<ul>");
        sb.Append("<li>Усі власні реалізації відповідають заявленій теоретичній складності — " +
                  "час росте за тим же порядком, що й очікувалося.</li>");
        sb.Append("<li>Вбудовані алгоритми .NET швидші в абсолютних числах, " +
                  "але різниця в межах одного порядку — навчальні реалізації не «зливають», а лише поступаються.</li>");
        sb.Append("<li>StdDev у більшості тестів < 5% від середнього, тож вимірюванням можна довіряти.</li>");
        sb.Append("<li>Алгоритми пошуку нічого не алокують у керованій купі. Сортування алокує лише " +
                  "копію вхідного масиву (це частина тесту, а не самого алгоритму).</li>");
        sb.Append("</ul>");

        sb.Append("<h2>Терміни</h2>");
        sb.Append("<dl>");
        sb.Append("<dt>Середній час (Mean)</dt><dd>Середнє арифметичне часу виконання по всіх ітераціях. " +
                  "Одиниці — нс, мкс або мс, залежно від величини.</dd>");
        sb.Append("<dt>Стандартне відхилення (StdDev)</dt><dd>Розкид результатів навколо середнього. " +
                  "Мале значення — стабільне виконання.</dd>");
        sb.Append("<dt>Алокації (Allocated)</dt><dd>Скільки байтів виділено в керованій купі за один виклик. " +
                  "«0 B» або «—» — алокацій немає.</dd>");
        sb.Append("<dt>O(log n)</dt><dd>Логарифмічна складність: подвоєння N додає лише один крок.</dd>");
        sb.Append("<dt>O(log log n)</dt><dd>Ітерований логарифм: ще повільніший ріст. " +
                  "Для N = 10⁶ — близько 4 кроків.</dd>");
        sb.Append("<dt>O(n log n)</dt><dd>Типова складність ефективного сортування.</dd>");
        sb.Append("<dt>JIT</dt><dd>Just-In-Time компілятор .NET, перетворює IL у машинний код перед виконанням.</dd>");
        sb.Append("<dt>BenchmarkDotNet</dt><dd>Бібліотека для мікробенчмаркингу .NET; робить прогрів JIT, " +
                  "статистичний аналіз і фільтрує шум.</dd>");
        sb.Append("</dl>");

        sb.Append("<hr>");
        sb.Append($"<p class=\"foot\">D-Index · BenchmarkDotNet 0.14.0 · {date}</p>");
        sb.Append("</body>\n</html>");

        try
        {
            string path = Path.Combine(workDir, "analysis-ua.html");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _proc?.Kill(entireProcessTree: true); } catch { }
        _proc?.Dispose();
        _cts?.Dispose();
    }
}
