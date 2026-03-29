using Web.Providers.Models.Enums;
using Web.Providers.Models.Tasks;
using Web.Providers.Schedule.Models.Payload;
using Web.Providers.Schedule.Models.Tasks;

namespace Web.Providers.Models
{
    public record GenerateScheduleRequest
    {
        public required IReadOnlyList<FixedTask> FixedTasks { get; init; }
        public required IReadOnlyList<DynamicTask> DynamicTasks { get; init; }
        public required PlanningHorizon PlanningHorizon { get; init; }
        public IReadOnlyList<CategoryWindow> CategoryWindows { get; init; } = [];
        public required DifficultTaskSchedulingStrategy DifficultTaskSchedulingStrategy { get; init; }
        public IReadOnlyList<DifficultyCapacityEntry> DifficultyCapacities { get; init; } = [];
        public IReadOnlyList<TaskTypePreferenceEntry> TaskTypePreferences { get; init; } = [];
    }
}
