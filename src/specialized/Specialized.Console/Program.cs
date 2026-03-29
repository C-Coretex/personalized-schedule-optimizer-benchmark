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

var solution = Solver.Solve(request.ToScheduleOptimizationRequest());