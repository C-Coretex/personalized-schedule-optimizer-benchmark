namespace Web.Features.Schedule.Endpoints.Submit;

public class Endpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/schedule/submit", async (
            Request request,
            Handler handler,
            ILogger<Endpoint> logger,
            CancellationToken ct) =>
        {
            try
            {
                await handler.Handle(request, ct);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to submit schedule result for job {JobId}", request.JobId);
                return Results.BadRequest(ex.Message);
            }
        })
        .AddEndpointFilter(async (ctx, next) =>
        {
            var config = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var expected = config["InternalApi:SharedSecret"];
            ctx.HttpContext.Request.Headers.TryGetValue("X-Internal-Token", out var actual);
            if (string.IsNullOrEmpty(expected) || !string.Equals(actual, expected, StringComparison.Ordinal))
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden);
            return await next(ctx);
        })
        .WithName("SubmitSchedule")
        .Produces(StatusCodes.Status200OK)
        .Produces<string>(StatusCodes.Status400BadRequest);
    }
}
