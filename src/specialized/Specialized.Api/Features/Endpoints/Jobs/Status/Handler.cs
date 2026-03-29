namespace Specialized.Api.Features.Endpoints.Jobs.Status;

public class Handler()
{
    public async Task<Response?> Handle(Guid jobId, CancellationToken ct)
    {
        var response = new Response(JobStatus.InProgress, new(0, 100000));

        return response;
    }
}