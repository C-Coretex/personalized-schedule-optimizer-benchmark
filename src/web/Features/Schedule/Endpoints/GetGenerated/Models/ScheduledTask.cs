using Web.Providers.Schedule.Models.Tasks;

namespace Web.Features.Schedule.Endpoints.GetGenerated.Models
{
    public record ScheduledTask<TTask>(DateTime Start, DateTime End, TTask Task) where TTask : TaskBase
    {
        public ScheduledTask<TaskBase> ToTaskBase() => new(Start, End, Task);
    }
}
