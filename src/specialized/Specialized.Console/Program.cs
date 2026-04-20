using Specialized.Console.Models;
using Specialized.Optimizer.Optimizer;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

var json = await File.ReadAllTextAsync("./input.json");

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters =
    {
        new JsonStringEnumConverter()
    }
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
        if (initialBytes == 0)
            continue;
        proc.Refresh();

        // Memory
        long currentBytes = proc.PrivateMemorySize64 - initialBytes;
        peakBytes = Math.Max(peakBytes, currentBytes);
        totalBytes += currentBytes;
        samples++;

        // CPU
        DateTime now = DateTime.UtcNow;
        TimeSpan currentCpuTime = proc.TotalProcessorTime;

        double cpuUsedMs = (currentCpuTime - lastCpuTime).TotalMilliseconds;
        double elapsedMs = (now - lastSampleTime).TotalMilliseconds;

        // Percentage across all logical cores (e.g. 400% max on 4-core machine)
        // Divide by logicalCores to get 0–100% "overall" usage
        double cpuPercent = elapsedMs > 0
            ? cpuUsedMs / (elapsedMs * logicalCores) * 100.0
            : 0;

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
solver.Solve(requestModel);
stopwatch.Stop();

await cts.CancelAsync();
try { await monitorTask; } catch (OperationCanceledException) { }

double peak = peakBytes / 1024.0 / 1024.0;
double averageMem = samples > 0 ? totalBytes / samples / 1024.0 / 1024.0 : 0;
double averageCpu = samples > 0 ? totalCpuPercent / samples : 0;

Console.WriteLine($"Elapsed time:           {stopwatch.Elapsed:mm\\:ss\\.fff}");
Console.WriteLine($"Logical cores:          {logicalCores}");
Console.WriteLine();
Console.WriteLine($"Peak private memory:    {peak:F2} MB");
Console.WriteLine($"Average private memory: {averageMem:F2} MB");
Console.WriteLine();
Console.WriteLine($"Peak CPU usage:         {peakCpuPercent:F1}%");
Console.WriteLine($"Average CPU usage:      {averageCpu:F1}%");
Console.WriteLine($"Samples taken:          {samples} (over ~{samples / 10.0:F1}s)");