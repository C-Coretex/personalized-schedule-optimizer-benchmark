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
        var optimizationTimeInSeconds = request.OptimizationTimeInSeconds;

        //construction
        planningDomain = ConstructionHeuristics.Construct(planningDomain, _random);

        //on Add we can either add to a free location or swap if there is no free location from task pool

        var moveSelector = new MoveSelector(_random);

        //optimization stage 1.
        var saStage = new SAEngine(moveSelector, _random, optimizationTimeInSeconds);
        planningDomain = saStage.Run(planningDomain);

        Console.WriteLine("SC1: " + planningDomain.SC1_TotalPriorityConstraint);
        Console.WriteLine("SC2: " + planningDomain.SC2_TotalConstraint);
        Console.WriteLine("SC3: " + planningDomain.SC3_TotalConstraint);
        Console.WriteLine("SC4: " + planningDomain.SC4_TotalConstraint);
        Console.WriteLine("SC5: " + planningDomain.SC5_MinimizeDifferenceFromWeekOptConstraint);
        Console.WriteLine("SC6: " + planningDomain.SC6_TotalConstraint);
        Console.WriteLine("SC7: " + planningDomain.SC7_TotalDifficultyDifference);

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
