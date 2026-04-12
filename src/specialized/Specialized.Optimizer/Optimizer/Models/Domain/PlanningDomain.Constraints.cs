using Specialized.Optimizer.Models.Enums;

namespace Specialized.Optimizer.Optimizer.Models.Domain;

//reactive constraint values and cache
//is being modified on planning variable modifications
internal partial record PlanningDomain
{
    //<Task, CountOfAvailableTasks(for repeating)>
    public Dictionary<Task, int> AvailableTasksPool { get; private set; } = [];
    public int HC1_TotalConstraint { get; private set; }

    public int HC2_RequiredTasksMustBeScheduledConstraint { get; private set; } = 0;

    public int HC7_TotalConstraint { get; private set; }

    private bool _isWeekRepeatingCountFieldCopied = true;
    //<WeekNumber, <TaskId, TaskCount>>
    public Dictionary<int, Dictionary<Guid, int>> WeekRepeatingTasksCount { get; private set; }

    public int HC6_RespectWeekMinOptCountConstraint { get; private set; } = 0;

    public int SC1_TotalPriorityConstraint { get; private set; } = 0;

    public int SC2_TotalConstraint { get; private set; } = 0;
    public int SC3_TotalConstraint { get; private set; } = 0;
    public int SC4_TotalConstraint { get; private set; } = 0;

    public int SC5_MinimizeDifferenceFromWeekOptConstraint { get; private set; } = 0;

    public int SC6_TotalConstraint { get; private set; } = 0;

    public int TotalDaysDifficultySum { get; private set; } = 0;
    public int SC7_TotalDifficultyDifference { get; private set; } = 0;

    public int SC8_TimeConsistencyConstraint { get; private set; } = 0;

    private void InitConstraintValues(Domain domain)
    {
        var weekTasks = domain.Tasks.OrderBy(t => t.Id).Where(t => t.IsWeekRepeating).ToArray();
        WeekRepeatingTasksCount = domain.Days.GroupBy(d => d.WeekNumber).OrderBy(g => g.Key).ToDictionary(g => g.Key, g =>
            weekTasks.OrderBy(t => t.Id).ToDictionary(wt => wt.Id, _ => 0));

        var nonRepeatingTasks = domain.Tasks.OrderBy(t => t.Id).Where(t => t.Repeating is null);

        var repeating = domain.Tasks.OrderBy(t => t.Id).Where(t => t.Repeating is not null)
            .SelectMany(t => Enumerable.Repeat(t, 
                Math.Min(t.Repeating!.OptWeekCount * WeekRepeatingTasksCount.Count, t.Repeating!.OptDayCount * domain.Days.Length)));

        AvailableTasksPool = nonRepeatingTasks.Concat(repeating)
            .GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

        HC2_RequiredTasksMustBeScheduledConstraint = domain.Tasks.Count(t => t.IsRequired && t.Repeating is null);

        foreach (var repeatingTask in domain.Tasks.Where(t => t.Repeating is not null))
        {
            HC6_RespectWeekMinOptCountConstraint += (repeatingTask.Repeating!.MinWeekCount ?? 0) * WeekRepeatingTasksCount.Count;
            SC5_MinimizeDifferenceFromWeekOptConstraint += repeatingTask.Repeating!.OptWeekCount * WeekRepeatingTasksCount.Count;
        }

        TotalDaysDifficultySum += PlanningDays.Sum(d => d.TotalDifficulty);
    }

    private void UpdateConstraintValues(ScheduledTask task, PlanningDay day, bool add)
    {
        if (add)
        {
            if (!AvailableTasksPool.TryGetValue(task.Task, out var count))
                throw new Exception("Task added that was not present in the Available Task Pool.");

            count--;
            if (count <= 0)
                AvailableTasksPool.Remove(task.Task);
            else
                AvailableTasksPool[task.Task] = count;
        }
        else
        {
            AvailableTasksPool.TryGetValue(task.Task, out var count);
            AvailableTasksPool[task.Task] = ++count;
        }

        var coefficient = add ? 1 : -1;

        if (task.Task.IsRequired && task.Task.Repeating is null)
        {
            HC2_RequiredTasksMustBeScheduledConstraint += -coefficient;
        }

        SC1_TotalPriorityConstraint += coefficient * (6 - task.Task.Priority);

        UpdateWeekMinOpt(task.Task, day.Day, add);

        var prevDayDifficulty = day.TotalDifficulty - (coefficient * task.Task.Difficulty);
        TotalDaysDifficultySum += day.TotalDifficulty - prevDayDifficulty;

        //if loops would be a hot path we could do batches for such constraints (that require full loop)
        //also SC7 and other totals could be done without loop if it would REALLY be a hot path
        var averageDifficulty = (double)TotalDaysDifficultySum / PlanningDays.Length;
        HC1_TotalConstraint = 0;
        HC7_TotalConstraint = 0;
        SC2_TotalConstraint = 0;
        SC3_TotalConstraint = 0;
        SC4_TotalConstraint = 0;
        SC6_TotalConstraint = 0;
        var sc7_totalDifficultyDifference = 0d;
        var repeatingStartSums = new Dictionary<Guid, (long Sum, long SumSq, int Count)>();
        //update all total constraints in one go
        foreach (var planningDay in PlanningDays)
        {
            var dayDifference = (planningDay.TotalDifficulty - averageDifficulty);
            sc7_totalDifficultyDifference += dayDifference * dayDifference;

            HC1_TotalConstraint += planningDay.HC1_NoTaskOverlaps;
            HC7_TotalConstraint += planningDay.HC7_RespectDayMinOptCountConstraint;
            SC2_TotalConstraint += planningDay.SC2_TotalDayDifficultyConstraint;
            SC3_TotalConstraint += planningDay.SC3_DifficultTaskSchedulingConstraint;
            SC4_TotalConstraint += planningDay.SC4_TypeWeightsConstraint;
            SC6_TotalConstraint += planningDay.SC6_MinimizeDifferenceFromDayOptConstraint;

            // SC8: collect start times of repeating task instances across all days
            foreach (var st in planningDay.ScheduledTasks)
            {
                if (st.Task.Repeating is null) continue;
                var m = st.Start.Hour * 60 + st.Start.Minute;
                repeatingStartSums.TryGetValue(st.Task.Id, out var cur);
                repeatingStartSums[st.Task.Id] = (cur.Sum + m, cur.SumSq + (long)m * m, cur.Count + 1);
            }
        }

        SC7_TotalDifficultyDifference = (int)Math.Ceiling(sc7_totalDifficultyDifference);

        // SC8: sum of start-time variance (minutes²) across all repeating tasks
        // Higher variance → more spread → worse score
        var sc8 = 0d;
        foreach (var (_, (sum, sumSq, count)) in repeatingStartSums)
        {
            if (count <= 1) continue;
            sc8 += (double)sumSq / count - (double)sum * sum / ((double)count * count);
        }
        SC8_TimeConsistencyConstraint = (int)Math.Ceiling(sc8);
    }

    private void UpdateWeekMinOpt(Task task, Day day, bool add)
    {
        if (task.Repeating is null)
            return;

        if(!_isWeekRepeatingCountFieldCopied)
        {
            WeekRepeatingTasksCount = WeekRepeatingTasksCount.ToDictionary(
                kvp => kvp.Key,
                kvp => new Dictionary<Guid, int>(kvp.Value));
            _isWeekRepeatingCountFieldCopied = true;
        }

        var coefficient = add ? 1 : -1;

        var weekRepeatingTasks = WeekRepeatingTasksCount[day.WeekNumber];

        var prevCount = weekRepeatingTasks[task.Id];
        var newCount = prevCount + coefficient;
        weekRepeatingTasks[task.Id] = newCount;

        var minWeekCount = task.Repeating!.MinWeekCount ?? 0;
        var optWeekCount = task.Repeating!.OptWeekCount;
        if (prevCount < minWeekCount || newCount < minWeekCount)
            HC6_RespectWeekMinOptCountConstraint -= coefficient;
        if (newCount > optWeekCount || prevCount > optWeekCount)
            HC6_RespectWeekMinOptCountConstraint += coefficient;

        SC5_MinimizeDifferenceFromWeekOptConstraint -= optWeekCount - prevCount;
        SC5_MinimizeDifferenceFromWeekOptConstraint += optWeekCount - newCount;
    }
}
