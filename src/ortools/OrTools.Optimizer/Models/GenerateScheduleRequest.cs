using OrTools.Optimizer.Models.Enums;
using OrTools.Optimizer.Models.Payload;
using OrTools.Optimizer.Models.Tasks;

namespace OrTools.Optimizer.Models;

public record GenerateScheduleRequest
{
    public required IReadOnlyList<FixedTask> FixedTasks { get; init; }
    public required IReadOnlyList<DynamicTask> DynamicTasks { get; init; }
    public required PlanningHorizon PlanningHorizon { get; init; }
    public IReadOnlyList<CategoryWindow> CategoryWindows { get; init; } = [];
    public required DifficultTaskSchedulingStrategy DifficultTaskSchedulingStrategy { get; init; }
    public IReadOnlyList<DifficultyCapacityEntry> DifficultyCapacities { get; init; } = [];
    public IReadOnlyList<TaskTypePreferenceEntry> TaskTypePreferences { get; init; } = [];
    public int OptimizationTimeInSeconds { get; init; } = 15;
}
