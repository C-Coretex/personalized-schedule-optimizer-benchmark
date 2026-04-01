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


        //for optimization Add step we should somehow collect task pool
        //it should contain all tasks that still can be scheduled(repeating tasks optimal value or just not scheduled)
        //on Add we can either add to a free location or swap if there is no free location

        //optimization stage 1.

        //optimization stage 2.

        var tasksTimeline = planningDomain.PlanningDays
            .SelectMany(d => d.ScheduledTasks.Select(st => new TaskResponse()
            {
                Id = st.Task.Id,
                StartTime = d.Day.Date.ToDateTime(st.Start),
                EndTime = d.Day.Date.ToDateTime(st.End)
            }));
        tasksTimeline = tasksTimeline.Concat(request.FixedTasks.Select(ft => new TaskResponse()
        {
            Id = ft.Id,
            StartTime = ft.StartTime,
            EndTime = ft.EndTime
        }));

        return new GenerateScheduleResponse() { TasksTimeline = tasksTimeline.OrderBy(st => st.StartTime).ToList() };
    }
}
