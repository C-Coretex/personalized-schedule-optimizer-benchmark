using Web.Providers.Models;

namespace Web.Features.Schedule.Models.Schedule
{
    public record ScheduleJobMetadata(Guid Id, GenerateScheduleRequest Request);
}
