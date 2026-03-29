using Specialized.Optimizer.Models;

namespace Specialized.Api.Features.Endpoints.Jobs.Run;

public class Handler()
{
    public async Task<Guid> Handle(GenerateScheduleRequest request, CancellationToken ct)
    {
        var jobId = Guid.CreateVersion7();

        //save the job id and the actual job. Also save input data for the job (to return it too).
        //on job finished - send job id + response to the callback specified in appsettings

        //on callback the web should calculate the score by himself

        return jobId;
    }
}