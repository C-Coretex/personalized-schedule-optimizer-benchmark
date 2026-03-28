namespace Web.Features.Schedule.Endpoints.GetGenerated;

public class Endpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/schedule/generated", async (Handler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(ct)))
            .WithName("GetGeneratedSchedules")
            .Produces<IReadOnlyList<Guid>>(StatusCodes.Status200OK);
    }
}
