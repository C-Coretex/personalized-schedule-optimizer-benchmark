using Web.Features.Schedule.Models.Schedule;

namespace Web.Features.Schedule.Endpoints.GetGenerated;

public class Endpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/schedule/generated", async (Handler handler, ILogger<Endpoint> logger, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await handler.Handle(ct));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to retrieve generated schedules");
                return Results.InternalServerError();
            }
        })
        .WithName("GetGeneratedSchedules")
        .Produces<IReadOnlyList<ScheduleJobMetadata>>(StatusCodes.Status200OK);
    }
}
