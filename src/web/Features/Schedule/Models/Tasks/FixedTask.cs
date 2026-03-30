using Web.Features.Schedule.Models.Enums;

namespace Web.Features.Schedule.Models.Tasks;

public record FixedTask : TaskBase
{
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }

    public Providers.Models.Tasks.FixedTask ToProviderModel() => new()
    {
        Id = Guid.NewGuid(),
        Name = Name,
        Priority = Priority,
        Difficulty = Difficulty,
        Types = Types.Select(t => t.ToProviderModel()).ToList(),
        StartTime = StartTime,
        EndTime = EndTime
    };
}
