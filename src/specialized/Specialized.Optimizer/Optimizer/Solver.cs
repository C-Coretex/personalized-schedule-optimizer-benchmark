using Specialized.Optimizer.Helpers;
using Specialized.Optimizer.Models;
using Specialized.Optimizer.Optimizer.Models.Domain;
using System.Globalization;

namespace Specialized.Optimizer.Optimizer;

public class Solver(int? seed = null)
{
    public Score? BestScore { get; private set; }

    private readonly Random _random = seed != null ? new Random(seed.Value) : new Random();

    public GenerateScheduleResponse Solve(GenerateScheduleRequest request)
    {
        //init
        var staticDomain = new Domain(request);
        var planningDomain = new PlanningDomain(staticDomain);

        //construction
        planningDomain = ConstructionHeuristics.Construct(planningDomain);

        //optimization stage 1.

        //optimization stage 2.

        return new GenerateScheduleResponse() { TasksTimeline = [] };
    }
}
