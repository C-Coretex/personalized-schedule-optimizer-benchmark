using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Web.Features.Schedule.Endpoints.Generate;

public class Handler(IHttpContextAccessor httpContextAccessor)
{
    public async Task<Guid> Handle(Request request, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();

        var session = httpContextAccessor.HttpContext?.Session;
        if (session is not null)
        {
            await session.LoadAsync(ct);

            var existing = session.GetString("schedule_ids");
            var ids = existing is not null
                ? JsonSerializer.Deserialize<List<Guid>>(existing)!
                : [];

            ids.Add(id);
            session.SetString("schedule_ids", JsonSerializer.Serialize(ids));

            await session.CommitAsync(ct);
        }

        return id;
    }
}
