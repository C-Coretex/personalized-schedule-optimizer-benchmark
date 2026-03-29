using System.Text.Json;
using System.Text.Json.Serialization;
using Web.Features.Schedule.Models.Schedule;
using Web.Providers;
using static Web.Providers.ServiceCollectionExtensions;

namespace Web.Features.Schedule.Endpoints.Generate;

public class Handler(
    IHttpContextAccessor httpContextAccessor, 
    [FromKeyedServices(OptimizationClients.Specialized)] IScheduleOptimizationClient scheduleOptimizationClient)
{
    public async Task<Guid> Handle(Request request, CancellationToken ct)
    {
        var scheduleOptimizationRequest = request.ToScheduleOptimizationRequest();
        var jobId = await scheduleOptimizationClient.GenerateSchedule(scheduleOptimizationRequest, ct);

        var session = httpContextAccessor.HttpContext?.Session;
        if (session is not null)
        {
            await session.LoadAsync(ct);

            var existing = session.GetString("schedule_ids");
            var ids = existing is not null
                ? JsonSerializer.Deserialize<List<ScheduleJobMetadata>>(existing)!//save GenerateRequestModel, not just the id
                : [];

            ids.Add(new(jobId, scheduleOptimizationRequest));
            session.SetString("schedule_ids", JsonSerializer.Serialize(ids));

            await session.CommitAsync(ct);
        }

        return jobId;
    }
}
