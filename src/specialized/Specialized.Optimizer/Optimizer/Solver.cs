using Specialized.Optimizer.Models;
using Specialized.Optimizer.Optimizer.Models.Domain;

namespace Specialized.Optimizer.Optimizer;

public class Solver
{
    public Score? BestScore { get; private set; }

    public GenerateScheduleResponse Solve(GenerateScheduleRequest request)
    {
        //init
        var domain = new Domain(request);

        //construction

        //optimization stage 1.

        //optimization stage 2.

        return new GenerateScheduleResponse() { TasksTimeline = [] };
    }

}
