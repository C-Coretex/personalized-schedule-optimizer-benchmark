namespace Web.Features.Schedule.Models.Schedule
{
    public class GeneratedSchedule
    {
        public required List<TaskResponse> TasksTimeline { get; init; }
    }

    public record TaskResponse
    {
        public required Guid Id { get; init; }
        public required DateTime StartTime { get; init; }
        public required DateTime EndTime { get; init; }
    }
}
