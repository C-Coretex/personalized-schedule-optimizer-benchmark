namespace Web.Providers.Models
{
    public record JobStatusResponse(JobStatus status, JobScore? score);

    public enum JobStatus
    {
        NotStarted,
        InProgress,
        CompletedOrNotFound
    }

    public record JobScore(int hardScore, int softScore);
}
