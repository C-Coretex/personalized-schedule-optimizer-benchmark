using Web.Features.Schedule.Models.Schedule;

namespace Web.Features.Schedule.Endpoints.Submit;

public record Request(Guid JobId, List<TaskResponse> TasksTimeline);
