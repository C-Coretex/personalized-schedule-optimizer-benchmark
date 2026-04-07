namespace OrTools.Api.Features.Endpoints.Jobs.Status;

public class Endpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/jobs/status", async (
            Guid id,
            Handler handler,
            ILogger<Endpoint> logger,
            CancellationToken ct) =>
        {
            try
            {
                var response = await handler.Handle(id, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get job status");
                return Results.BadRequest(ex.Message);
            }
        })
        .WithName("GetStatus")
        .Produces<Response?>(StatusCodes.Status200OK)
        .Produces<string>(StatusCodes.Status400BadRequest);
    }
}
