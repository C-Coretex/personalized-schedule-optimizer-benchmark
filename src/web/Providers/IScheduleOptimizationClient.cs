using Web.Providers.Models;

namespace Web.Providers
{
    public interface IScheduleOptimizationClient
    {
        Task<Guid> GenerateSchedule(GenerateScheduleRequest request, CancellationToken ct = default);
        Task<JobStatusResponse> GetStatus(Guid jobId, CancellationToken ct = default);
        Task<CurrentSolutionResponse> GetCurrentSolution(Guid jobId, CancellationToken ct = default);
    }
}
