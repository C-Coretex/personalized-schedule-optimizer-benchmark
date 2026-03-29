using Web.Providers.Models;

namespace Web.Providers.Clients
{
    public class SpecializedScheduleOptimizationClient(HttpClient client, ILogger<SpecializedScheduleOptimizationClient> logger) : IScheduleOptimizationClient
    {
        public async Task<Guid> GenerateSchedule(GenerateScheduleRequest request, CancellationToken ct = default)
        {
            var response = await client.PostAsJsonAsync("/jobs/run", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("GenerateSchedule failed. Status: {StatusCode}, Body: {Body}", response.StatusCode, body);
            }
            response.EnsureSuccessStatusCode();

            var jobId = await response.Content.ReadFromJsonAsync<Guid>(ct);
            return jobId;
        }

        public Task<CurrentSolutionResponse> GetCurrentSolution(Guid jobId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<JobStatusResponse> GetStatus(Guid jobId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
