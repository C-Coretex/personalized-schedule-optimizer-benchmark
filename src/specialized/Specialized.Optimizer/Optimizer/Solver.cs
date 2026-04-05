using Specialized.Optimizer.Models;
using Specialized.Optimizer.Models.Tasks;
using Specialized.Optimizer.Optimizer.Helpers;
using Specialized.Optimizer.Optimizer.Models.Domain;
using Specialized.Optimizer.Optimizer.Moves;

namespace Specialized.Optimizer.Optimizer;

public class Solver
{
    public Solver(int? seed = null)
    {
        _random = seed != null ? new Random(seed.Value) : new Random(111111);
    }

    public Score? BestScore { get; private set; }

    private readonly Random _random;

    public GenerateScheduleResponse Solve(GenerateScheduleRequest request)
    {
        //init
        var staticDomain = new Domain(request);
        var planningDomain = new PlanningDomain(staticDomain);

        //construction
        planningDomain = ConstructionHeuristics.Construct(planningDomain, _random);

        var constraintScore = planningDomain.CalculateConstraintScore();

        //on Add we can either add to a free location or swap if there is no free location from task pool

        var moveSelector = new MoveSelector(_random);

        //optimization stage 1.
        var saStage = new SAStage(moveSelector, _random);
        planningDomain = saStage.Run(planningDomain);

        var constraintScore2 = planningDomain.CalculateConstraintScore();
        //optimization stage 2.
        //probably run in parallel if several differentiating versions present
        //probably we don't need to combine LAHC and SA
        // var lahcStage = new LAHCStage(moveEngine);
        // planningDomain = lahcStage.Run(planningDomain);

        return PrepareReturnModel(request, planningDomain);
    }

    private GenerateScheduleResponse PrepareReturnModel(GenerateScheduleRequest request, PlanningDomain domain)
    {
        var tasksTimeline = domain.PlanningDays
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
