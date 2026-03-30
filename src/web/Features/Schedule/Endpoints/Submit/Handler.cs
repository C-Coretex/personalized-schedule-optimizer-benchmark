using Microsoft.Extensions.Caching.Memory;
using Web.Features.Schedule.Models.Schedule;

namespace Web.Features.Schedule.Endpoints.Submit;

public class Handler(IMemoryCache cache)
{
    public Task Handle(Request request, CancellationToken ct)
    {
        var schedule = new GeneratedSchedule { TasksTimeline = request.TasksTimeline };
        cache.Set($"job_result_{request.JobId}", schedule,
            new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(120) });
        return Task.CompletedTask;
    }
}
