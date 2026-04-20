using OrTools.Console.Models.Enums;
using OrTools.Console.Models.Payload;
using OrTools.Console.Models.Tasks;
using OrTools.Optimizer.Models;

namespace OrTools.Console.Models;

internal record Request
{
    public int? OptimizationTimeInSeconds { get; init; }
    public int NumSearchWorkers { get; init; } = 1;
    public required IReadOnlyList<FixedTask> FixedTasks { get; init; }
    public required IReadOnlyList<DynamicTask> DynamicTasks { get; init; }
    public required PlanningHorizon PlanningHorizon { get; init; }
    public IReadOnlyList<CategoryWindow> CategoryWindows { get; init; } = [];
    public required DifficultTaskSchedulingStrategy DifficultTaskSchedulingStrategy { get; init; }
    public IReadOnlyList<DifficultyCapacityEntry> DifficultyCapacities { get; init; } = [];
    public IReadOnlyList<TaskTypePreferenceEntry> TaskTypePreferences { get; init; } = [];

    public GenerateScheduleRequest ToScheduleOptimizationRequest() => new()
    {
        OptimizationTimeInSeconds = OptimizationTimeInSeconds ?? 15,
        NumSearchWorkers = NumSearchWorkers,
        FixedTasks = FixedTasks.Select(t => t.ToProviderModel()).ToList(),
        DynamicTasks = DynamicTasks.Select(t => t.ToProviderModel()).ToList(),
        PlanningHorizon = PlanningHorizon.ToProviderModel(),
        CategoryWindows = CategoryWindows.Select(w => w.ToProviderModel()).ToList(),
        DifficultTaskSchedulingStrategy = DifficultTaskSchedulingStrategy.ToProviderModel(),
        DifficultyCapacities = DifficultyCapacities.Select(d => d.ToProviderModel()).ToList(),
        TaskTypePreferences = TaskTypePreferences.Select(p => p.ToProviderModel()).ToList()
    };
}
