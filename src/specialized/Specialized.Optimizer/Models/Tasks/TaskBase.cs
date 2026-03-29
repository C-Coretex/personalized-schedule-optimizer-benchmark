using Specialized.Optimizer.Models.Enums;

namespace Specialized.Optimizer.Models.Tasks;

public abstract record TaskBase
{
    public required Guid Id { get; init; }

    /// <summary>Priority from 1 (lowest) to 5 (highest).</summary>
    public required int Priority { get; init; }

    /// <summary>Difficulty from 1 (trivial) to 10 (hardest).</summary>
    public required int Difficulty { get; init; }

    public required IReadOnlyList<TaskType> Types { get; init; }
}
