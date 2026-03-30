using Web.Features.Schedule.Models.Enums;

namespace Web.Features.Schedule.Models.Tasks;

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

    public Providers.Schedule.Models.Tasks.DynamicTask ToProviderModel() => new()
    {
        Id = Guid.NewGuid(),
        Name = Name,
        Priority = Priority,
        Difficulty = Difficulty,
        Types = Types.Select(t => t.ToProviderModel()).ToList(),
        IsRequired = IsRequired,
        Duration = Duration,
        WindowStart = WindowStart,
        WindowEnd = WindowEnd,
        Deadline = Deadline,
        Categories = Categories.Select(c => c.ToProviderModel()).ToList(),
        Repeating = Repeating?.ToProviderModel()
    };
}
