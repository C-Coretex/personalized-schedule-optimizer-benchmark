using OrTools.Optimizer.Models.Tasks;

namespace OrTools.Optimizer.Models;

public record GenerateScheduleResponse
{
    public required List<TaskResponse> TasksTimeline { get; init; }
}
