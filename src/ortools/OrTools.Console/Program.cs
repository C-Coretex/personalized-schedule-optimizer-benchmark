using OrTools.Console;
using OrTools.Console.Models;
using OrTools.Optimizer.Models.Tasks;
using OrTools.Optimizer.Optimizer;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

var json = await File.ReadAllTextAsync("./input.json");

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() }
};
var request = JsonSerializer.Deserialize<Request>(json, jsonOptions)!;

var proc = Process.GetCurrentProcess();

long initialBytes = 0;
long peakBytes = 0;
long totalBytes = 0;
double peakCpuPercent = 0;
double totalCpuPercent = 0;
TimeSpan lastCpuTime = proc.TotalProcessorTime;
DateTime lastSampleTime = DateTime.UtcNow;
int logicalCores = Environment.ProcessorCount;
int samples = 0;

using var cts = new CancellationTokenSource();

var monitorTask = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        try { await Task.Delay(100, cts.Token); }
        catch (OperationCanceledException) { break; }
        if (initialBytes == 0) continue;
        proc.Refresh();

        long currentBytes = proc.PrivateMemorySize64 - initialBytes;
        peakBytes = Math.Max(peakBytes, currentBytes);
        totalBytes += currentBytes;
        samples++;

        DateTime now = DateTime.UtcNow;
        TimeSpan currentCpuTime = proc.TotalProcessorTime;
        double cpuUsedMs = (currentCpuTime - lastCpuTime).TotalMilliseconds;
        double elapsedMs = (now - lastSampleTime).TotalMilliseconds;
        double cpuPercent = elapsedMs > 0 ? cpuUsedMs / (elapsedMs * logicalCores) * 100.0 : 0;
        peakCpuPercent = Math.Max(peakCpuPercent, cpuPercent);
        totalCpuPercent += cpuPercent;
        lastCpuTime = currentCpuTime;
        lastSampleTime = now;
    }
}, cts.Token);

var requestModel = request.ToScheduleOptimizationRequest();

var stopwatch = Stopwatch.StartNew();
var solver = new Solver();
initialBytes = proc.PrivateMemorySize64;
var response = solver.Solve(requestModel);
stopwatch.Stop();

cts.Cancel();
try { await monitorTask; } catch (OperationCanceledException) { }

// ── Timeline ─────────────────────────────────────────────────────────────────

var byId = requestModel.FixedTasks.Cast<TaskBase>()
    .Concat(requestModel.DynamicTasks)
    .ToDictionary(t => t.Id);

var scheduledItems = response.TasksTimeline
    .Select(tt => (TaskResponse: tt, Task: byId.TryGetValue(tt.Id, out var t) ? t : null))
    .Where(x => x.Task is not null)
    .OrderBy(x => x.TaskResponse.StartTime)
    .ToList();

var scheduledIds = scheduledItems.Select(x => x.Task!.Id).ToHashSet();
var unscheduled = requestModel.DynamicTasks.Where(t => !scheduledIds.Contains(t.Id)).ToList();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                     SCHEDULE TIMELINE                       ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

DateOnly? lastDay = null;
foreach (var (tt, task) in scheduledItems)
{
    var day = DateOnly.FromDateTime(tt.StartTime);
    if (day != lastDay)
    {
        Console.WriteLine($"\n  {day:ddd, MMM dd}");
        lastDay = day;
    }
    var kind = task is DynamicTask dt
        ? (dt.IsRequired ? "[Required]" : "[Optional]")
        : "[Fixed]   ";
    Console.WriteLine($"    {tt.StartTime:HH:mm} – {tt.EndTime:HH:mm}  {kind}  P:{task!.Priority} D:{task.Difficulty:D2}  {task.Name}");
}

if (unscheduled.Count > 0)
{
    Console.WriteLine($"\n  ── Unscheduled ({unscheduled.Count}) ──────────────────────────────────");
    foreach (var t in unscheduled)
    {
        var tag = t.Repeating is not null ? "[Repeating]" : t.IsRequired ? "[Required] " : "[Optional] ";
        Console.WriteLine($"    {tag}  P:{t.Priority} D:{t.Difficulty:D2}  {t.Name}");
    }
}

// ── Scores ───────────────────────────────────────────────────────────────────

var score = ScheduleScorer.Calculate(requestModel, response);

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                    CONSTRAINT SCORES                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

Console.WriteLine("\n  Hard Constraints:");
foreach (var c in score.HardConstraints)
    Console.WriteLine($"    {c.Id,-4}  {(c.Score == 0 ? "✓" : "✗")}  {c.Score,6}  {c.Name}");

Console.WriteLine($"\n  Total Hard Score: {score.TotalHard}  {(score.TotalHard == 0 ? "← FEASIBLE" : "← VIOLATIONS")}");

Console.WriteLine("\n  Soft Constraints:");
foreach (var c in score.SoftConstraints)
    Console.WriteLine($"    {c.Id,-4}       {c.Score,6}  {c.Name}");

Console.WriteLine($"\n  Total Soft Score: {score.TotalSoft}");

// ── Perf ─────────────────────────────────────────────────────────────────────

double peak = peakBytes / 1024.0 / 1024.0;
double averageMem = samples > 0 ? totalBytes / samples / 1024.0 / 1024.0 : 0;
double averageCpu = samples > 0 ? totalCpuPercent / samples : 0;

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                      PERFORMANCE                            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine($"  Elapsed time:           {stopwatch.Elapsed:mm\\:ss\\.fff}");
Console.WriteLine($"  Logical cores:          {logicalCores}");
Console.WriteLine($"  Peak private memory:    {peak:F2} MB");
Console.WriteLine($"  Average private memory: {averageMem:F2} MB");
Console.WriteLine($"  Peak CPU usage:         {peakCpuPercent:F1}%");
Console.WriteLine($"  Average CPU usage:      {averageCpu:F1}%");
Console.WriteLine($"  Samples taken:          {samples} (over ~{samples / 10.0:F1}s)");
