using Google.OrTools.ModelBuilder;
using Google.OrTools.Sat;
using OrTools.Optimizer.Models;
using OrTools.Optimizer.Models.Tasks;

namespace OrTools.Optimizer.Optimizer;

public class Solver
{
    private record DynamicTaskVars(
        DynamicTask Task,
        IntVar Start,
        IntVar End,
        IntervalVar Interval,
        BoolVar? Presence // null = required (always scheduled)
    );

    public GenerateScheduleResponse Solve(GenerateScheduleRequest request)
    {
        var model = new CpModel();

        var horizonOrigin = request.PlanningHorizon.StartDate.ToDateTime(TimeOnly.MinValue);
        int horizonMax = ToMin(request.PlanningHorizon.EndDate.ToDateTime(TimeOnly.MaxValue));

        var allIntervals = new List<IntervalVar>();
        var dynamicTaskVars = new List<DynamicTaskVars>();


        AddFixedTasks();

        AddDynamicTasks();

        HC1();
        //HC2 - ensured automatically
        //HC4 - ensured automatically
        //HC6 - ensured automatically
        //HC8 - ensured automatically
        //HC9 - ensured automatically



        SC1();

        var solver = new CpSolver
        {
            StringParameters = $"num_search_workers:8, max_time_in_seconds:{request.OptimizationTimeInSeconds}"
        };
        var status = solver.Solve(model);

        var timeline = new List<TaskResponse>();
        if (status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
        {
            foreach (var v in dynamicTaskVars)
            {
                bool isScheduled = v.Presence is null || solver.BooleanValue(v.Presence);
                if (!isScheduled) continue;

                var startTime = horizonOrigin.AddMinutes(solver.Value(v.Start));
                var endTime = startTime.AddMinutes(v.Task.Duration);
                timeline.Add(new TaskResponse { Id = v.Task.Id, StartTime = startTime, EndTime = endTime });
            }

            foreach (var ft in request.FixedTasks)
                timeline.Add(new TaskResponse { Id = ft.Id, StartTime = ft.StartTime, EndTime = ft.EndTime });
        }

        return new GenerateScheduleResponse { TasksTimeline = timeline };


        int ToMin(DateTime dt) => (int)(dt - horizonOrigin).TotalMinutes;

        // Fixed tasks — pinned intervals with no decision variables
        void AddFixedTasks()
        {
            foreach (var fixedTask in request.FixedTasks)
            {
                int start = ToMin(fixedTask.StartTime);
                int end = ToMin(fixedTask.EndTime);
                int duration = end - start;
                var startVar = model.NewIntVar(start, start, $"fs_{fixedTask.Id}");
                var endVar = model.NewIntVar(end, end, $"fe_{fixedTask.Id}");
                var interval = model.NewIntervalVar(startVar, duration, endVar, $"fi_{fixedTask.Id}");
                allIntervals.Add(interval);
            }
        }

        // Dynamic tasks (non-repeating only)
        void AddDynamicTasks()
        {
            foreach (var dynamicTask in request.DynamicTasks)
            {
                var dynamicTaskDeadline = dynamicTask.Deadline is not null ? (int?)ToMin(dynamicTask.Deadline.Value) : null;

                var startVar = model.NewIntVar(0, horizonMax - dynamicTask.Duration, $"start_{dynamicTask.Id}");
                var endVar = model.NewIntVar(dynamicTask.Duration, dynamicTaskDeadline ?? horizonMax, $"end_{dynamicTask.Id}");

                if (dynamicTask.IsRequired)
                {
                    AddRequiredTask(startVar, endVar);
                }
                else if (dynamicTask.Repeating is null)
                {
                    AddOptionalTask(startVar, endVar);
                }
                else
                {
                    //add min day to every day
                    if (dynamicTask.Repeating.MinDayCount > 0)
                    {
                        var days = request.PlanningHorizon.GetDays();
                        foreach (var day in days)
                        {
                            var start = ToMin(day.ToDateTime(TimeOnly.MinValue));
                            var end = ToMin(day.ToDateTime(TimeOnly.MaxValue));

                            var startVarTemp = model.NewIntVar(start, end - dynamicTask.Duration, $"start_{dynamicTask.Id}_dm_i");
                            var endVarTemp = model.NewIntVar(start + dynamicTask.Duration, end, $"start_{dynamicTask.Id}_dm_i");

                            for (var i = 0; i < dynamicTask.Repeating.MinDayCount; i++)
                            {
                                AddRequiredTask(startVarTemp, endVarTemp, $"_dm_{i}");
                            }
                        }
                    }

                    //add min week to every week (if any left)

                    var weeks = request.PlanningHorizon.GetWeeks();
                    foreach (var week in weeks)
                    {
                        var days = week.End.Day - week.Start.Day;
                        var dayTasksAdded = dynamicTask.Repeating.MinDayCount ?? 0 * days;
                        var requiredTasksToAdd = (dynamicTask.Repeating.MinWeekCount ?? 0) - dayTasksAdded;
                        requiredTasksToAdd = Math.Min(0, requiredTasksToAdd);
                        // if (tasksToAdd <= 0)
                        //    continue;

                        var start = ToMin(week.Start.ToDateTime(TimeOnly.MinValue));
                        var end = ToMin(week.End.ToDateTime(TimeOnly.MaxValue));

                        var startVarTemp = model.NewIntVar(start, end - dynamicTask.Duration, $"start_{dynamicTask.Id}_wm_i");
                        var endVarTemp = model.NewIntVar(start + dynamicTask.Duration, end, $"start_{dynamicTask.Id}_wm_i");

                        for (var i = 0; i < requiredTasksToAdd; i++)
                        {
                            AddRequiredTask(startVarTemp, endVarTemp, $"_wm_{i}");
                        }

                        var optionalTasksToAdd = dynamicTask.Repeating.OptWeekCount - (dayTasksAdded + requiredTasksToAdd);
                        for (var i = 0; i < optionalTasksToAdd; i++)
                        {
                            AddOptionalTask(startVarTemp, endVarTemp, $"_wo_{i}");
                        }
                    }
                }

                void AddRequiredTask(IntVar start, IntVar end, string postfix = "")
                {
                    var interval = model.NewIntervalVar(start, dynamicTask.Duration, end, $"iv_{dynamicTask.Id}{postfix}");
                    allIntervals.Add(interval);
                    dynamicTaskVars.Add(new DynamicTaskVars(dynamicTask, start, end, interval, null));
                }
                void AddOptionalTask(IntVar start, IntVar end, string postfix = "")
                {
                    var presence = model.NewBoolVar($"presence_{dynamicTask.Id}{postfix}");
                    var interval = model.NewOptionalIntervalVar(start, dynamicTask.Duration, end, presence, $"iv_{dynamicTask.Id}{postfix}");
                    allIntervals.Add(interval);
                    dynamicTaskVars.Add(new DynamicTaskVars(dynamicTask, start, end, interval, presence));
                }
            }
        }

        // HC1: No task time overlaps
        void HC1() => model.AddNoOverlap(allIntervals);

        void HC3()
        {

        }

        void HC5()
        {

        }

        //ensure only days opt count (as we've set just for WeekOptCount)
        void HC7()
        {

        }



        // SC1: Maximize total priority of scheduled tasks
        void SC1()
        {
            var linearExpressionBuilder = Google.OrTools.Sat.LinearExpr.NewBuilder();
            foreach (var dynamicTaskVar in dynamicTaskVars)
            {
                if (dynamicTaskVar.Presence is null)
                    linearExpressionBuilder.Add(6 - dynamicTaskVar.Task.Priority);
                else
                    linearExpressionBuilder.AddTerm(dynamicTaskVar.Presence, 6 - dynamicTaskVar.Task.Priority);
            }
            model.Maximize(linearExpressionBuilder);
        }
    }
}
