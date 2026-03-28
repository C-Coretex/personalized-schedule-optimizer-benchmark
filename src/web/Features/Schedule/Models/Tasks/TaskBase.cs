using Web.Features.Schedule.Models.Enums;

namespace Web.Features.Schedule.Models.Tasks;

public abstract record TaskBase
{
    /// <summary>Priority from 1 (lowest) to 5 (highest).</summary>
    public required int Priority { get; init; }

    /// <summary>Difficulty from 1 (trivial) to 10 (hardest).</summary>
    public required int Difficulty { get; init; }

    public required IReadOnlyList<TaskType> Types { get; init; }
}
