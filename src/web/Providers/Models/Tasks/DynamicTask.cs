using Web.Providers.Models.Enums;
using Web.Providers.Models.Tasks;

namespace Web.Providers.Schedule.Models.Tasks;

public record DynamicTask : TaskBase
{
    public required bool IsRequired { get; init; }

    /// <summary>Duration in minutes.</summary>
    public required int Duration { get; init; }

    public required TimeOnly? WindowStart { get; init; }
    public required TimeOnly? WindowEnd { get; init; }

    /// <summary>Hard deadline. Null means no deadline.</summary>
    public DateTime? Deadline { get; init; }

    public IReadOnlyList<Category> Categories { get; init; } = [];

    /// <summary>Null means the task does not repeat.</summary>
    public RepeatingSchedule? Repeating { get; init; }
}
