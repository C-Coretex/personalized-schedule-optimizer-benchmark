using Specialized.Optimizer.Models.Tasks;

namespace Specialized.Optimizer.Models
{
    public record GenerateScheduleResponse
    {
        public required List<TaskResponse> TasksTimeline { get; init; }
    }
}
