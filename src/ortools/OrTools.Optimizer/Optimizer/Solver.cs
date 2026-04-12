using Google.OrTools.LinearSolver;
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
        IntVar DayIndex,
        IntVar TimeFromDayStart,
        int WindowStart, 
        int WindowEnd,
        BoolVar? Presence // null = required (always scheduled)
    );
    private const int minutesPerDay = 24 * 60;

    public GenerateScheduleResponse Solve(GenerateScheduleRequest request)
    {
        var model = new CpModel();

        var horizonOrigin = request.PlanningHorizon.StartDate.ToDateTime(TimeOnly.MinValue);
        int horizonMax = ToMin(request.PlanningHorizon.EndDate.ToDateTime(TimeOnly.MaxValue));

        var allIntervals = new List<IntervalVar>();
        var dynamicTaskVars = new List<DynamicTaskVars>();
        var isOnDayMap = new Dictionary<(DynamicTaskVars, int), BoolVar>();
        var scheduledOnDayMap = new Dictionary<(DynamicTaskVars, int), BoolVar>();

        var objective = Google.OrTools.Sat.LinearExpr.NewBuilder();

        AddFixedTasks();

        AddDynamicTasks();

        HC1();
        //HC2 - ensured automatically
        HC3();
        //HC4 - ensured automatically
        HC5();
        //HC6 - ensured automatically
        HC7();
        //HC8 - ensured automatically
        //HC9 - ensured automatically



        SC1();
       // SC2();
        SC3();
        SC4();
        SC5();
        SC6();
        SC7();

        model.Minimize(objective);

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

        int TimeOnlyToMinutes(TimeOnly to) => to.Hour * 60 + to.Minute;

        int DayToIndex(DateOnly day)
            => day.DayNumber - request.PlanningHorizon.StartDate.DayNumber;

        BoolVar GetIsOnDay(DynamicTaskVars taskVar, int dayIndex)
        {
            if (isOnDayMap.TryGetValue((taskVar, dayIndex), out var existing))
                return existing;

            var isOnDay = model.NewBoolVar($"onDay_{taskVar.Task.Id}_{dayIndex}");

            model.Add(taskVar.DayIndex == dayIndex).OnlyEnforceIf(isOnDay);
            model.Add(taskVar.DayIndex != dayIndex).OnlyEnforceIf(isOnDay.Not());

            isOnDayMap[(taskVar, dayIndex)] = isOnDay;
            return isOnDay;
        }

        BoolVar GetScheduledOnDay(DynamicTaskVars taskVar, int dayIndex)
        {
            if (scheduledOnDayMap.TryGetValue((taskVar, dayIndex), out var existing))
                return existing;

            var isOnDay = GetIsOnDay(taskVar, dayIndex);

            BoolVar result;

            if (taskVar.Presence is null)
            {
                // required task → scheduled == isOnDay
                result = isOnDay;
            }
            else
            {
                result = model.NewBoolVar($"scheduledOnDay_{taskVar.Task.Id}_{dayIndex}");
                model.AddMultiplicationEquality(result, [taskVar.Presence, isOnDay]);
            }

            scheduledOnDayMap[(taskVar, dayIndex)] = result;
            return result;
        }

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
            foreach (var task in request.DynamicTasks)
            {
                var deadline = task.Deadline is not null ? (int?)ToMin(task.Deadline.Value) : null;

                if (task.Repeating is null)
                {
                    AddSingleTask(task, 0, horizonMax, deadline, required: task.IsRequired);
                    continue;
                }

                AddRepeatingTask(task, deadline);
            }

            void AddRepeatingTask(DynamicTask task, int? deadline)
            {
                var days = request.PlanningHorizon.GetDays();
                var weeks = request.PlanningHorizon.GetWeeks();

                var minDayCount = task.Repeating?.MinDayCount ?? 0;
                var minWeekCount = task.Repeating?.MinWeekCount ?? 0;
                var optWeekCount = task.Repeating?.OptWeekCount ?? 0;

                // 1) Required occurrences for every day
                foreach (var day in days)
                {
                    var dayStart = ToMin(day.ToDateTime(TimeOnly.MinValue));
                    var dayEnd = ToMin(day.ToDateTime(TimeOnly.MaxValue));

                    for (var i = 0; i < minDayCount; i++)
                    {
                        AddSingleTask(
                            task,
                            dayStart,
                            dayEnd,
                            deadline,
                            required: true,
                            postfix: $"_d{day.DayNumber}_{i}");
                    }
                }

                // 2) Required weekly occurrences only for the missing part
                foreach (var week in weeks)
                {
                    var weekStart = ToMin(week.Start.ToDateTime(TimeOnly.MinValue));
                    var weekEnd = ToMin(week.End.ToDateTime(TimeOnly.MaxValue));

                    var dayRequiredAlready = minDayCount * (week.End.DayNumber - week.Start.DayNumber);
                    var missingWeeklyRequired = Math.Max(0, minWeekCount - dayRequiredAlready);

                    for (var i = 0; i < missingWeeklyRequired; i++)
                    {
                        AddSingleTask(
                            task,
                            weekStart,
                            weekEnd,
                            deadline,
                            required: true,
                            postfix: $"_w{week.Start.DayNumber}_{i}");
                    }

                    // 3) Optional extra weekly tasks
                    var totalRequired = dayRequiredAlready + missingWeeklyRequired;
                    var optionalCount = Math.Max(0, optWeekCount - totalRequired);

                    for (var i = 0; i < optionalCount; i++)
                    {
                        AddSingleTask(
                            task,
                            weekStart,
                            weekEnd,
                            deadline,
                            required: false,
                            postfix: $"_wo{week.Start.DayNumber}_{i}");
                    }
                }
            }

            void AddSingleTask(
                DynamicTask task,
                int windowStart,
                int windowEnd,
                int? deadline,
                bool required,
                string postfix = "")
            {
                var latestEnd = deadline.HasValue ? Math.Min(windowEnd, deadline.Value) : windowEnd;
                var latestStart = latestEnd - task.Duration;

                if (latestStart < windowStart)
                    latestStart = windowStart;

                if (latestEnd - task.Duration < windowStart)
                    return;

                var startVar = model.NewIntVar(windowStart, latestStart, $"start_{task.Id}{postfix}");
                var endVar = model.NewIntVar(windowStart + task.Duration, latestEnd, $"end_{task.Id}{postfix}");

                var maxDayIndex = horizonMax / minutesPerDay;
                IntVar dayIndex = model.NewIntVar(0, maxDayIndex, $"day_{task.Id}{postfix}");
                //TimeOnly minute of day
                IntVar startMod = model.NewIntVar(0, minutesPerDay - 1, $"startMod_{task.Id}{postfix}");

                model.AddDivisionEquality(dayIndex, startVar, minutesPerDay);
                model.AddModuloEquality(startMod, startVar, minutesPerDay);

                BoolVar? presence = null;
                if (required)
                {
                    var interval = model.NewIntervalVar(startVar, task.Duration, endVar, $"iv_{task.Id}{postfix}");
                    allIntervals.Add(interval);
                    dynamicTaskVars.Add(new DynamicTaskVars(task, startVar, endVar, interval, dayIndex, startMod, windowStart, windowEnd, null));
                }
                else
                {
                    presence = model.NewBoolVar($"presence_{task.Id}{postfix}");
                    var interval = model.NewOptionalIntervalVar(startVar, task.Duration, endVar, presence, $"iv_{task.Id}{postfix}");
                    allIntervals.Add(interval);
                    dynamicTaskVars.Add(new DynamicTaskVars(task, startVar, endVar, interval, dayIndex, startMod, windowStart, windowEnd, presence));
                }
            }
        }

        // HC1: No task time overlaps
        void HC1() => model.AddNoOverlap(allIntervals);

        //tasks must be scheduled within their time windows
        void HC3()
        {
            foreach (var dynamicTaskVar in dynamicTaskVars)
            {
                if (dynamicTaskVar.Task.WindowStart is not null)
                {
                    var ws = TimeOnlyToMinutes(dynamicTaskVar.Task.WindowStart.Value);
                    var constraint = model.Add(dynamicTaskVar.TimeFromDayStart >= ws);
                    if (dynamicTaskVar.Presence is not null)
                        constraint.OnlyEnforceIf(dynamicTaskVar.Presence);
                }
                if (dynamicTaskVar.Task.WindowEnd is not null)
                {
                    var we = TimeOnlyToMinutes(dynamicTaskVar.Task.WindowEnd.Value);
                    var constraint = model.Add(dynamicTaskVar.TimeFromDayStart + dynamicTaskVar.Task.Duration <= we);
                    if (dynamicTaskVar.Presence is not null)
                        constraint.OnlyEnforceIf(dynamicTaskVar.Presence);
                }
            }
        }

        //task cannot be scheduled outside its category time window
        void HC5()
        {
            //HC5
            foreach (var dynamicTaskVar in dynamicTaskVars)
            {
                var fits = new List<BoolVar>();
                var dayCategoryTimeWindows = request.CategoryWindows.Where(cw => dynamicTaskVar.Task.Categories.Contains(cw.Category))
                    .GroupBy(cw => cw.StartDateTime.Date).OrderBy(g => g.Key);
                foreach (var dayTimeWindows in dayCategoryTimeWindows)
                {
                    foreach (var window in dayTimeWindows)
                    {
                        var fit = model.NewBoolVar($"fit_{dynamicTaskVar.Task.Id}_{dayTimeWindows.Key}_{window.Category}_{window.StartDateTime}");
                        fits.Add(fit);

                        model.Add(dynamicTaskVar.DayIndex == DayToIndex(DateOnly.FromDateTime(dayTimeWindows.Key))).OnlyEnforceIf(fit);

                        var ws = TimeOnlyToMinutes(TimeOnly.FromDateTime(window.StartDateTime));
                        var we = TimeOnlyToMinutes(TimeOnly.FromDateTime(window.EndDateTime));

                        model.Add(dynamicTaskVar.TimeFromDayStart >= ws).OnlyEnforceIf(fit);
                        model.Add(dynamicTaskVar.TimeFromDayStart + dynamicTaskVar.Task.Duration <= we).OnlyEnforceIf(fit);
                    }
                }

                var constraintCat = model.Add(Google.OrTools.Sat.LinearExpr.Sum(fits) == 1);
                if (dynamicTaskVar.Presence is not null)
                    constraintCat.OnlyEnforceIf(dynamicTaskVar.Presence);
            }
        }

        //ensure only days opt count (as we've set just for WeekOptCount)
        void HC7()
        {
            var days = request.PlanningHorizon.GetDays().ToList();

            foreach (var taskGroup in dynamicTaskVars.GroupBy(v => v.Task))
            {
                var optDayCount = taskGroup.Key.Repeating?.OptDayCount;
                if (optDayCount is null)
                    continue;

                foreach (var day in days)
                {
                    int dayIndex = DayToIndex(day);

                    var dayOccurrences = new List<BoolVar>();

                    foreach (var taskVar in taskGroup)
                    {
                        // 1 if this copy is scheduled on this day, otherwise 0
                        var isOnDay = GetIsOnDay(taskVar, dayIndex);

                        if (taskVar.Presence is null)
                        {
                            // required copy
                            dayOccurrences.Add(isOnDay);
                        }
                        else
                        {
                            // optional copy: counted only if present AND on this day
                            var scheduledOnDay = GetScheduledOnDay(taskVar, dayIndex);
                            dayOccurrences.Add(scheduledOnDay);
                        }
                    }

                    model.Add(Google.OrTools.Sat.LinearExpr.Sum(dayOccurrences) <= optDayCount.Value);
                }
            }
        }


        // SC1: Maximize total priority of scheduled tasks
        void SC1()
        {
            foreach (var dynamicTaskVar in dynamicTaskVars)
            {
                var weight = (6 - dynamicTaskVar.Task.Priority) * 100 * -1;
                if (dynamicTaskVar.Presence is null)
                    objective.Add(weight);
                else
                    objective.AddTerm(dynamicTaskVar.Presence, weight);
            }
        }

        //minimize total difficulty of scheduled tasks above daily difficulty capacities
        void SC2()
        {
            var maxPossibleDailyDifficulty = dynamicTaskVars.Sum(v => v.Task.Difficulty);

            foreach (var dc in request.DifficultyCapacities)
            {
                var dayIndex = DayToIndex(dc.Date);

                var dayDifficulty = Google.OrTools.Sat.LinearExpr.NewBuilder();

                foreach (var taskVar in dynamicTaskVars)
                {
                    var isOnDay = GetIsOnDay(taskVar, dayIndex);

                    var weight = taskVar.Task.Difficulty * 500;
                    if (taskVar.Presence is null)
                    {
                        dayDifficulty.AddTerm(isOnDay, weight);
                    }
                    else
                    {
                        var scheduledOnDay = GetScheduledOnDay(taskVar, dayIndex);
                        dayDifficulty.AddTerm(scheduledOnDay, taskVar.Task.Difficulty);
                    }
                }

                var overCapacity = model.NewIntVar(0, maxPossibleDailyDifficulty, $"sc2_over_{dc.Date:yyyyMMdd}");
                model.Add(overCapacity >= dayDifficulty - dc.Capacity);
                model.Add(overCapacity >= 0);
            }
        }

        //follow strategy for scheduling difficult tasks
        void SC3()
        {

        }

        //maximize user defined task type preferences
        void SC4()
        {

        }

        //minimize difference between actual and optimal number of scheduled occurrences for week repeating tasks
        void SC5()
        {

        }

        //minimize difference between actual and optimal number of scheduled occurrences for day repeating tasks
        void SC6()
        {

        }

        //minimize difficulty difference between days
        void SC7()
        {

        }
    }
}
