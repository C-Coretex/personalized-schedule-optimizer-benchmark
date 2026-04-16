using Web.Features.Schedule.Models.Enums;
using Web.Features.Schedule.Models.Payload;
using Web.Features.Schedule.Models.Tasks;
using Web.Providers;
using Web.Providers.Models;
using static Web.Providers.ServiceCollectionExtensions;

namespace Web.Features.Schedule.Endpoints.Generate;

public record Request
{
    public required IReadOnlyList<FixedTask> FixedTasks { get; init; }
    public required IReadOnlyList<DynamicTask> DynamicTasks { get; init; }
    public required PlanningHorizon PlanningHorizon { get; init; }
    public IReadOnlyList<CategoryWindow> CategoryWindows { get; init; } = [];
    public required DifficultTaskSchedulingStrategy DifficultTaskSchedulingStrategy { get; init; }
    public IReadOnlyList<DifficultyCapacityEntry> DifficultyCapacities { get; init; } = [];
    public IReadOnlyList<TaskTypePreferenceEntry> TaskTypePreferences { get; init; } = [];
    public int? OptimizationTimeInSeconds { get; init; }
    public OptimizationClients Optimizer { get; init; } = OptimizationClients.Specialized;

    public GenerateScheduleRequest ToScheduleOptimizationRequest()
    {
        var days = PlanningHorizon.EndDate.DayNumber - PlanningHorizon.StartDate.DayNumber + 1;
        var defaultSeconds = days >= 30 ? 30 : 15;
        var optimizationTime = Math.Clamp(OptimizationTimeInSeconds ?? defaultSeconds, 1, 300);

        return new()
        {
            FixedTasks = FixedTasks.Select(t => t.ToProviderModel()).ToList(),
            DynamicTasks = DynamicTasks.Select(t => t.ToProviderModel()).ToList(),
            PlanningHorizon = PlanningHorizon.ToProviderModel(),
            CategoryWindows = CategoryWindows.Select(w => w.ToProviderModel()).ToList(),
            DifficultTaskSchedulingStrategy = DifficultTaskSchedulingStrategy.ToProviderModel(),
            DifficultyCapacities = DifficultyCapacities.Select(d => d.ToProviderModel()).ToList(),
            TaskTypePreferences = TaskTypePreferences.Select(p => p.ToProviderModel()).ToList(),
            OptimizationTimeInSeconds = optimizationTime
        };
    }
}
