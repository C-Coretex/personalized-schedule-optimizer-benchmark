using Specialized.Optimizer.Models;
using Specialized.Optimizer.Optimizer;

namespace Specialized.Api.Features.Endpoints.Jobs.Run;

public class Handler(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<Handler> logger)
{
    public async Task<Guid> Handle(GenerateScheduleRequest request, CancellationToken ct)
    {
        var jobId = Guid.CreateVersion7();

        _ = Task.Run(async () =>
        {
            var solver = new Solver();
            var response = solver.Solve(request);

            var callbackUrl = configuration["Callbacks:ScheduleSubmitUrl"];
            if (string.IsNullOrEmpty(callbackUrl))
            {
                logger.LogInformation("No callback URL configured. Job {JobId} result will not be sent.", jobId);
                return;
            }

            try
            {
                var client = httpClientFactory.CreateClient();
                var secret = configuration["InternalApi:SharedSecret"];
                if (!string.IsNullOrEmpty(secret))
                    client.DefaultRequestHeaders.Add("X-Internal-Token", secret);
                var payload = new { JobId = jobId, response.TasksTimeline };

                var httpResponse = await client.PostAsJsonAsync(callbackUrl, payload);
                httpResponse.EnsureSuccessStatusCode();

                logger.LogInformation("Job {JobId} result sent to callback successfully.", jobId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send job {JobId} result to callback URL {Url}.", jobId, callbackUrl);
            }
        }, ct);

        return jobId;
    }
}
