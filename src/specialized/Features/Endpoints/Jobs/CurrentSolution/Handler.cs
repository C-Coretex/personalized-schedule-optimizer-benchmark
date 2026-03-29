namespace Specialized.Features.Endpoints.Jobs.CurrentSolution;

public class Handler()
{
    public async Task<Response?> Handle(Guid jobId, CancellationToken ct)
    {
        return new Response();
    }
}
