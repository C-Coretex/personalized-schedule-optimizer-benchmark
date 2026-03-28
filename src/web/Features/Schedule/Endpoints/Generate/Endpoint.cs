namespace Web.Features.Schedule.Endpoints.Generate
{
    public class Endpoint
    {
        public static void Map(WebApplication app)
        {
            app.MapPost("/schedule/generate", async (
                Request request,
                Handler handler,
                CancellationToken ct) =>
            {
                if (!Validator.IsValid(request, out var error))
                    return Results.BadRequest(error);

                try
                {
                    var id = await handler.Handle(request, ct);
                    return Results.Ok(id);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            })
            .WithName("GenerateSchedule")
            .Produces<Guid>(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status400BadRequest);
        }
    }
}
