namespace OrTools.Api.Features.Endpoints.Jobs.Status;

public record Response(JobStatus status, JobScore? score);

public enum JobStatus
{
    NotStarted,
    InProgress,
    CompletedOrNotFound
}

public record JobScore(int hardScore, int softScore);
