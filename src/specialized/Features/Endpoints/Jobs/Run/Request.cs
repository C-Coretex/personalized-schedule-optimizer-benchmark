using Specialized.Features.Models.Enums;
using Specialized.Features.Models.Payload;
using Specialized.Features.Models.Tasks;

namespace Specialized.Features.Endpoints.Jobs.Run;

public record Request
{
    public required IReadOnlyList<FixedTask> FixedTasks { get; init; }
    public required IReadOnlyList<DynamicTask> DynamicTasks { get; init; }
    public required PlanningHorizon PlanningHorizon { get; init; }
    public IReadOnlyList<CategoryWindow> CategoryWindows { get; init; } = [];
    public required DifficultTaskSchedulingStrategy DifficultTaskSchedulingStrategy { get; init; }
    public IReadOnlyList<DifficultyCapacityEntry> DifficultyCapacities { get; init; } = [];
    public IReadOnlyList<TaskTypePreferenceEntry> TaskTypePreferences { get; init; } = [];
}
