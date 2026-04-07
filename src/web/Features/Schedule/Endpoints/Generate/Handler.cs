using System.Text.Json;
using Web.Features.Schedule.Models.Schedule;
using Web.Providers;
using static Web.Providers.ServiceCollectionExtensions;

namespace Web.Features.Schedule.Endpoints.Generate;

public class Handler(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
{
    public async Task<Guid> Handle(Request request, CancellationToken ct)
    {
        var scheduleOptimizationRequest = request.ToScheduleOptimizationRequest();
        var client = serviceProvider.GetRequiredKeyedService<IScheduleOptimizationClient>(request.Optimizer);
        var jobId = await client.GenerateSchedule(scheduleOptimizationRequest, ct);

        var session = httpContextAccessor.HttpContext?.Session;
        if (session is not null)
        {
            await session.LoadAsync(ct);

            var existing = session.GetString("schedule_data");
            var data = existing is not null
                ? JsonSerializer.Deserialize<List<ScheduleJobMetadata>>(existing)!
                : [];

            data.Add(new(jobId, scheduleOptimizationRequest, null, request.Optimizer));
            session.SetString("schedule_data", JsonSerializer.Serialize(data));

            await session.CommitAsync(ct);
        }

        return jobId;
    }
}
