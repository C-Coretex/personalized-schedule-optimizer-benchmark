using System.Text.Json;
using Web.Features.Schedule.Models.Schedule;

namespace Web.Features.Schedule.Endpoints.GetGenerated;

public class Handler(IHttpContextAccessor httpContextAccessor)
{
    public async Task<IReadOnlyList<ScheduleJobMetadata>> Handle(CancellationToken ct)
    {
        var session = httpContextAccessor.HttpContext?.Session;
        if (session is null) return [];

        await session.LoadAsync(ct);
        var scheduleIdsJson = session.GetString("schedule_ids");
        return !string.IsNullOrEmpty(scheduleIdsJson)
                ? JsonSerializer.Deserialize<List<ScheduleJobMetadata>>(scheduleIdsJson)! : [];
    }
}
