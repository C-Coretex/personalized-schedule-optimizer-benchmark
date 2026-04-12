using Google.OrTools.LinearSolver;
using Google.OrTools.Sat;
using OrTools.Optimizer.Models;
using OrTools.Optimizer.Models.Enums;
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

        //we want to multiply every SC except SC7 by n, since we can't divide SC7/n because of integer vals
        int numDays = request.PlanningHorizon.GetDays().Count();

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
        SC2();
        SC3();
        SC4();
        SC5();
        SC6();
        SC7();

        model.Minimize(objective);

        var solver = new CpSolver
        {
            StringParameters = $"num_search_workers:1, max_time_in_seconds:{request.OptimizationTimeInSeconds}"
        };
        var status = solver.Solve(model);


        return PrepareResponse();

        //private functions
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

        BoolVar GetScheduledBool(DynamicTaskVars taskVar, int dayIndex)
            => taskVar.Presence is null
                ? GetIsOnDay(taskVar, dayIndex)
                : GetScheduledOnDay(taskVar, dayIndex);

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
                var weight = (6 - dynamicTaskVar.Task.Priority) * 100 * numDays * -1;
                if (dynamicTaskVar.Presence is null)
                    objective.Add(weight);
                else
                    objective.AddTerm(dynamicTaskVar.Presence, weight);
            }
        }

        //minimize total difficulty of scheduled tasks above daily difficulty capacities
        void SC2()
        {
            var maxPossibleDailyDifficulty = dynamicTaskVars.Sum(v => v.Task.Difficulty)
                + request.FixedTasks.Sum(ft => ft.Difficulty);

            foreach (var dc in request.DifficultyCapacities)
            {
                var dayIndex = DayToIndex(dc.Date);
                var dayDate = dc.Date.ToDateTime(TimeOnly.MinValue).Date;

                var dayDifficulty = Google.OrTools.Sat.LinearExpr.NewBuilder();

                // Fixed tasks on this day
                foreach (var ft in request.FixedTasks)
                {
                    if (ft.StartTime.Date == dayDate)
                        dayDifficulty.Add(ft.Difficulty);
                }

                // Dynamic tasks
                foreach (var taskVar in dynamicTaskVars)
                {
                    if (taskVar.Presence is null)
                    {
                        var isOnDay = GetIsOnDay(taskVar, dayIndex);
                        dayDifficulty.AddTerm(isOnDay, taskVar.Task.Difficulty);
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
                objective.AddTerm(overCapacity, 500 * numDays);
            }
        }

        //follow strategy for scheduling difficult tasks
        void SC3()
        {
            //Consecutive gap sum = span − durations. If you sort n tasks by start time, the sum of gaps between neighbors telescopes to end[last] − start[first] − Σdurations. This avoids modeling pairwise ordering in CP-SAT entirely.
            //Conditional min/max via sentinels. Inactive tasks get start = horizonMax (won't win the min) and end = 0 (won't win the max), so AddMinEquality/AddMaxEquality naturally select only active tasks.
            int coefficient = request.DifficultTaskSchedulingStrategy == DifficultTaskSchedulingStrategy.Cluster ? 1 : -1;

            foreach (var day in request.PlanningHorizon.GetDays())
            {
                int dayIndex = DayToIndex(day);
                var dayDate = day.ToDateTime(TimeOnly.MinValue).Date;

                var effStarts = new List<IntVar>();
                var effEnds = new List<IntVar>();
                var activeList = new List<BoolVar>();
                int fixedCount = 0;
                int fixedDurSum = 0;
                var durTerms = Google.OrTools.Sat.LinearExpr.NewBuilder();

                // Fixed difficult tasks pinned to this day (always active)
                foreach (var ft in request.FixedTasks.Where(ft => ft.Difficulty >= 6 && ft.StartTime.Date == dayDate))
                {
                    int start = ToMin(ft.StartTime);
                    int end = ToMin(ft.EndTime);

                    effStarts.Add(model.NewIntVar(start, start, $"sc3_fs_{ft.Id}_{dayIndex}"));
                    effEnds.Add(model.NewIntVar(end, end, $"sc3_fe_{ft.Id}_{dayIndex}"));
                    fixedDurSum += (end - start);
                    fixedCount++;
                }

                // Dynamic difficult tasks that might land on this day
                foreach (var dtv in dynamicTaskVars.Where(v => v.Task.Difficulty >= 6))
                {
                    var active = GetScheduledBool(dtv, dayIndex);
                    activeList.Add(active);

                    // When inactive, push start to horizonMax (won't be the min)
                    var es = model.NewIntVar(0, horizonMax, $"sc3_es_{dtv.Task.Id}_{dayIndex}");
                    model.Add(es == dtv.Start).OnlyEnforceIf(active);
                    model.Add(es == horizonMax).OnlyEnforceIf(active.Not());
                    effStarts.Add(es);

                    // When inactive, push end to 0 (won't be the max)
                    var ee = model.NewIntVar(0, horizonMax, $"sc3_ee_{dtv.Task.Id}_{dayIndex}");
                    model.Add(ee == dtv.End).OnlyEnforceIf(active);
                    model.Add(ee == 0).OnlyEnforceIf(active.Not());
                    effEnds.Add(ee);

                    durTerms.AddTerm(active, dtv.Task.Duration);
                }

                int totalCandidates = effStarts.Count;
                if (totalCandidates < 2) continue;

                // Count of active difficult tasks on this day
                var countBuilder = Google.OrTools.Sat.LinearExpr.NewBuilder();
                countBuilder.Add(fixedCount);
                foreach (var a in activeList)
                    countBuilder.AddTerm(a, 1);

                var count = model.NewIntVar(0, totalCandidates, $"sc3_cnt_{dayIndex}");
                model.Add(count == countBuilder);

                var hasMultiple = model.NewBoolVar($"sc3_multi_{dayIndex}");
                model.Add(count >= 2).OnlyEnforceIf(hasMultiple);
                model.Add(count <= 1).OnlyEnforceIf(hasMultiple.Not());

                // Conditional min/max across all candidates
                var minStart = model.NewIntVar(0, horizonMax, $"sc3_min_{dayIndex}");
                var maxEnd = model.NewIntVar(0, horizonMax, $"sc3_max_{dayIndex}");
                model.AddMinEquality(minStart, effStarts);
                model.AddMaxEquality(maxEnd, effEnds);

                // Total duration of active difficult tasks on this day
                durTerms.Add(fixedDurSum);
                var sumDur = model.NewIntVar(0, totalCandidates * minutesPerDay, $"sc3_dur_{dayIndex}");
                model.Add(sumDur == durTerms);

                // Consecutive gap sum = span - durations (only when 2+ tasks)
                var gap = model.NewIntVar(0, minutesPerDay, $"sc3_gap_{dayIndex}");
                model.Add(gap == maxEnd - minStart - sumDur).OnlyEnforceIf(hasMultiple);
                model.Add(gap == 0).OnlyEnforceIf(hasMultiple.Not());

                objective.AddTerm(gap, coefficient * numDays);
            }
        }

        //maximize user defined task type preferences
        void SC4()
        {
            var prefsByDay = request.TaskTypePreferences.ToDictionary(p => p.Date, p => p.Preferences);

            foreach (var day in request.PlanningHorizon.GetDays())
            {
                if (!prefsByDay.TryGetValue(day, out var prefs)) continue;
                int dayIndex = DayToIndex(day);

                foreach (var dtv in dynamicTaskVars)
                {
                    int matchWeight = prefs
                        .Where(p => dtv.Task.Types.Contains(p.Type))
                        .Sum(p => p.Weight);

                    if (matchWeight == 0) continue;

                    var scheduled = GetScheduledBool(dtv, dayIndex);
                    objective.AddTerm(scheduled, -1 * matchWeight * numDays);
                }
            }
        }

        //minimize difference between actual and optimal number of scheduled occurrences for week repeating tasks
        void SC5()
        {
            var weeks = request.PlanningHorizon.GetWeeks().ToList();

            foreach (var taskGroup in dynamicTaskVars.GroupBy(v => v.Task))
            {
                var rep = taskGroup.Key.Repeating;
                if (rep is null || rep.OptWeekCount <= 0) continue;

                foreach (var week in weeks)
                {
                    int weekStartIdx = DayToIndex(week.Start);
                    int weekEndIdx = DayToIndex(week.End);

                    var inWeekBools = new List<BoolVar>();

                    foreach (var taskVar in taskGroup)
                    {
                        // Each task var lives on exactly one day, so summing
                        // GetScheduledBool across the week's days gives 0 or 1
                        for (int d = weekStartIdx; d <= weekEndIdx; d++)
                        {
                            inWeekBools.Add(GetScheduledBool(taskVar, d));
                        }
                    }

                    // deficit = max(0, OptWeekCount - weeklyCount)
                    // Achieved by: deficit >= 0 (domain), deficit >= opt - count (constraint),
                    // and the minimizer pushes it down to the true max.
                    var deficit = model.NewIntVar(0, rep.OptWeekCount,
                        $"sc5_def_{taskGroup.Key.Id}_{week.Start}");
                    model.Add(deficit >= rep.OptWeekCount - Google.OrTools.Sat.LinearExpr.Sum(inWeekBools));

                    objective.AddTerm(deficit, 50 * numDays);
                }
            }
        }

        //minimize difference between actual and optimal number of scheduled occurrences for day repeating tasks
        void SC6()
        {
            var days = request.PlanningHorizon.GetDays().ToList();

            foreach (var taskGroup in dynamicTaskVars.GroupBy(v => v.Task))
            {
                var rep = taskGroup.Key.Repeating;
                if (rep is null || rep.OptDayCount <= 0) continue;

                foreach (var day in days)
                {
                    int dayIndex = DayToIndex(day);

                    var dayBools = new List<BoolVar>();

                    foreach (var taskVar in taskGroup)
                    {
                        dayBools.Add(GetScheduledBool(taskVar, dayIndex));
                    }

                    var deficit = model.NewIntVar(0, rep.OptDayCount,
                        $"sc6_def_{taskGroup.Key.Id}_{day}");
                    model.Add(deficit >= rep.OptDayCount - Google.OrTools.Sat.LinearExpr.Sum(dayBools));

                    objective.AddTerm(deficit, 50 * numDays);
                }
            }
        }

        //minimize difficulty difference between days
        void SC7()
        {
            var days = request.PlanningHorizon.GetDays().ToList();
            int numDays = days.Count;
            if (numDays < 2) return;

            var maxDailyDiff = dynamicTaskVars.Sum(v => v.Task.Difficulty)
                + request.FixedTasks.Sum(ft => ft.Difficulty);

            var dayDiffVars = new List<IntVar>();

            foreach (var day in days)
            {
                int dayIndex = DayToIndex(day);
                var dayDate = day.ToDateTime(TimeOnly.MinValue).Date;

                var diffExpr = Google.OrTools.Sat.LinearExpr.NewBuilder();

                // Fixed tasks on this day
                foreach (var ft in request.FixedTasks)
                {
                    if (ft.StartTime.Date == dayDate)
                        diffExpr.Add(ft.Difficulty);
                }

                // Dynamic tasks
                foreach (var dtv in dynamicTaskVars)
                {
                    diffExpr.AddTerm(GetScheduledBool(dtv, dayIndex), dtv.Task.Difficulty);
                }

                var dayDiff = model.NewIntVar(0, maxDailyDiff, $"sc7_diff_{dayIndex}");
                model.Add(dayDiff == diffExpr);
                dayDiffVars.Add(dayDiff);
            }

            // n * Σdᵢ²
            foreach (var (dayDiff, idx) in dayDiffVars.Select((d, i) => (d, i)))
            {
                var sq = model.NewIntVar(0, (long)maxDailyDiff * maxDailyDiff, $"sc7_sq_{idx}");
                model.AddMultiplicationEquality(sq, [dayDiff, dayDiff]);
                objective.AddTerm(sq, numDays);
            }

            // - (Σdᵢ)²
            long maxTotal = (long)maxDailyDiff * numDays;
            var totalDiff = model.NewIntVar(0, maxTotal, "sc7_total");
            model.Add(totalDiff == Google.OrTools.Sat.LinearExpr.Sum(dayDiffVars));

            var totalSq = model.NewIntVar(0, maxTotal * maxTotal, "sc7_totalSq");
            model.AddMultiplicationEquality(totalSq, [totalDiff, totalDiff]);

            objective.AddTerm(totalSq, -1);
        }

        GenerateScheduleResponse PrepareResponse()
        {
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
        }
    }
}
