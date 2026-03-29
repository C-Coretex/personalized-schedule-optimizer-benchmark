namespace Specialized.Features.Endpoints.Jobs.Run;

public class Endpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/jobs/run", async (
            Request request,
            Handler handler,
            ILogger<Endpoint> logger,
            CancellationToken ct) =>
        {
            try
            {
                var id = await handler.Handle(request, ct);
                return Results.Ok(id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate schedule");
                return Results.BadRequest(ex.Message);
            }
        })
        .WithName("GenerateSchedule")
        .Produces<Guid>(StatusCodes.Status200OK)
        .Produces<string>(StatusCodes.Status400BadRequest);
    }
}
