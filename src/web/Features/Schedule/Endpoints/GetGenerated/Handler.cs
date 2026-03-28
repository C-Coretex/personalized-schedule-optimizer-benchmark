using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Web.Features.Schedule.Endpoints.GetGenerated;

public class Handler(IHttpContextAccessor httpContextAccessor)
{
    public async Task<IReadOnlyList<Guid>> Handle(CancellationToken ct)
    {
        var session = httpContextAccessor.HttpContext?.Session;
        if (session is null) return [];

        await session.LoadAsync(ct);
        var scheduleIdsJson = session.GetString("schedule_ids");
        return !string.IsNullOrEmpty(scheduleIdsJson)
                ? JsonSerializer.Deserialize<List<Guid>>(scheduleIdsJson)! : [];
    }
}
