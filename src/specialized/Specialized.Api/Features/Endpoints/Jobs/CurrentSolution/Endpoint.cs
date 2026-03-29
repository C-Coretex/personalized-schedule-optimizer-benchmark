namespace Specialized.Api.Features.Endpoints.Jobs.CurrentSolution;

public class Endpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/jobs/current-solution", async (
            Guid id,
            Handler handler,
            CancellationToken ct) =>
        {
            try
            {
                var response = await handler.Handle(id, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ex.Message);
            }
        })
        .WithName("CurrentSolution")
        .Produces<Response?>(StatusCodes.Status200OK)
        .Produces<string>(StatusCodes.Status400BadRequest);
    }
}
