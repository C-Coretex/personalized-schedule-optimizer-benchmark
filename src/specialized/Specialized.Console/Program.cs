using Specialized.Console.Models;
using Specialized.Optimizer.Optimizer;
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

var solver = new Solver();
var task = Task.Run(() => solver.Solve(request.ToScheduleOptimizationRequest()));

await task;