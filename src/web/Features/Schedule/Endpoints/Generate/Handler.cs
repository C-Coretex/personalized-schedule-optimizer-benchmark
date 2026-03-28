namespace Web.Features.Schedule.Endpoints.Generate;

public class Handler()
{
    public async Task<Guid> Handle(Request request, CancellationToken ct)
    {
        return await Task.FromResult(Guid.CreateVersion7());
    }
}
