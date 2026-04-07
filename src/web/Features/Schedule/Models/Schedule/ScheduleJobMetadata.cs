using Web.Providers.Models;
using static Web.Providers.ServiceCollectionExtensions;

namespace Web.Features.Schedule.Models.Schedule
{
    public record ScheduleJobMetadata(Guid Id, GenerateScheduleRequest Request, GeneratedSchedule? Response, OptimizationClients Optimizer);
}
