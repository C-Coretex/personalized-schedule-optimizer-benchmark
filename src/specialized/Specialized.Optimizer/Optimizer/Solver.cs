using Specialized.Optimizer.Models;
using Specialized.Optimizer.Models.Tasks;
using Specialized.Optimizer.Optimizer.Models.Domain;

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
        planningDomain = ConstructionHeuristics.Construct(planningDomain, _random);

        //optimization stage 1.

        //optimization stage 2.

        var tasksTimeline = planningDomain.PlanningDays
            .SelectMany(d => d.ScheduledTasks.OrderBy(st => st.Start).Select(st => new TaskResponse()
            {
                Id = st.Task.Id,
                StartTime = d.Day.Date.ToDateTime(st.Start),
                EndTime = d.Day.Date.ToDateTime(st.End)
            }));
        return new GenerateScheduleResponse() { TasksTimeline = tasksTimeline.ToList() };
    }
}
