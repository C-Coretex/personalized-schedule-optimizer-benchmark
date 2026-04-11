using OrTools.Console.Models.Enums;

namespace OrTools.Console.Models.Tasks;

public record FixedTask : TaskBase
{
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }

    public Optimizer.Models.Tasks.FixedTask ToProviderModel() => new()
    {
        Id = Guid.NewGuid(),
        Priority = Priority,
        Difficulty = Difficulty,
        Types = Types.Select(t => t.ToProviderModel()).ToList(),
        StartTime = StartTime,
        EndTime = EndTime
    };
}
