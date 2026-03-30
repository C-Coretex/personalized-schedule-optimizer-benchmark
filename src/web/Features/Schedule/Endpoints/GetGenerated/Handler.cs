using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Web.Features.Schedule.Models.Schedule;

namespace Web.Features.Schedule.Endpoints.GetGenerated;

public class Handler(IHttpContextAccessor httpContextAccessor, IMemoryCache cache)
{
    public async Task<IReadOnlyList<ScheduleJobMetadata>> Handle(CancellationToken ct)
    {
        var session = httpContextAccessor.HttpContext?.Session;
        if (session is null) return [];

        await session.LoadAsync(ct);
        var scheduleIdsJson = session.GetString("schedule_data");
        if (string.IsNullOrEmpty(scheduleIdsJson)) return [];

        var jobs = JsonSerializer.Deserialize<List<ScheduleJobMetadata>>(scheduleIdsJson)!;
        return jobs.Select(meta =>
        {
            if (meta.Response is null && cache.TryGetValue($"job_result_{meta.Id}", out GeneratedSchedule? schedule))
                return meta with { Response = schedule };
            return meta;
        }).ToList();
    }
}
