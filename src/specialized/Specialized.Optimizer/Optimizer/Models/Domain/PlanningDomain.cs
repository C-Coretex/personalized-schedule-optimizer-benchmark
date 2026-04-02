using System.Collections.Frozen;

namespace Specialized.Optimizer.Optimizer.Models.Domain;

//separate from Domain to optimize snapshots
internal record PlanningDomain
{
    public PlanningDomain(Domain domain)
    {
        Domain = domain;

        var weekTasks = domain.Tasks.Where(t => t.IsWeekRepeating).ToArray();
        WeekRepeatingTasksCount = domain.Days.GroupBy(d => d.WeekNumber).ToDictionary(g => g.Key, g =>
            weekTasks.ToDictionary(wt => wt.Id, _ => 0));

        var nonRepeatingTasks = domain.Tasks.Where(t => !t.IsDayRepeating && !t.IsWeekRepeating);

        var dayRepeating = domain.Tasks.Where(t => t.IsDayRepeating && !t.IsWeekRepeating)
            .SelectMany(t => Enumerable.Repeat(t, t.Repeating!.OptDayCount * domain.Days.Length));
        var weekRepeating = domain.Tasks.Where(t => t.IsWeekRepeating) //don't filter by day as WeekOptCount will be higher anyway
            .SelectMany(t => Enumerable.Repeat(t, t.Repeating!.OptWeekCount * WeekRepeatingTasksCount.Count));

        AvailableTasksPool = nonRepeatingTasks.Concat(dayRepeating).Concat(weekRepeating)
            .GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());

        PlanningDays = [.. domain.Days.Select(day => new PlanningDay(day, this))];

        HC2_RequiredTasksMustBeScheduledConstraint = domain.Tasks.Count(t => t.IsRequired && !t.IsDayRepeating && !t.IsWeekRepeating);
    }

    public void OnTaskAdded(ScheduledTask task, PlanningDay day)
    {
        UpdateConstraintValues(task, day, add: true);

        if (!AvailableTasksPool.TryGetValue(task.Task, out var count))
            throw new Exception("Task added that was not present in the Available Task Pool.");

        count--;
        if(count <= 0)
            AvailableTasksPool.Remove(task.Task);
        else
            AvailableTasksPool[task.Task] = count;
    }

    public void OnTaskRemoved(ScheduledTask task, PlanningDay day)
    {
        UpdateConstraintValues(task, day, add: false);

        AvailableTasksPool.TryGetValue(task.Task, out var count);
        AvailableTasksPool[task.Task] = count++;
    }

    public Domain Domain { get; init; }

    //actual planning entity
    public PlanningDay[] PlanningDays { get; set; } = [];

    public Dictionary<Task, int> AvailableTasksPool { get; init; } = [];

    //reactive constraint values and cache
    //is being modified on planning variable modifications
    #region Constraint Values

    public int HC2_RequiredTasksMustBeScheduledConstraint { get; set; } = 0;

    //<WeekNumber, <TaskId, TaskCount>>
    public Dictionary<int, Dictionary<Guid, int>> WeekRepeatingTasksCount { get; set; }

    public int HC6_RespectWeekMinOptCountConstraint { get; set; } = 0;

    public int SC1_TotalPriorityConstraint { get; set; } = 0;

    public int SC5_MinimizeDifferenceFromWeekOptConstraint { get; set; } = 0;

    public int TotalDaysDifficultySum { get; set; } = 0;

    public int SC7_TotalDifficultyDifference { get; set; } = 0;


    #endregion

    public PlanningDomain GetSnapshot()
    {
        var snapshot = this with
        {
            AvailableTasksPool = new(AvailableTasksPool),
            WeekRepeatingTasksCount = new(WeekRepeatingTasksCount)
        };
        snapshot.PlanningDays = [.. PlanningDays.Select(pd => pd.GetSnapshot(snapshot))];

        return snapshot;
    }

    private void UpdateConstraintValues(ScheduledTask task, PlanningDay day, bool add)
    {
        var coefficient = add ? 1 : -1;

        if (task.Task.IsRequired && !task.Task.IsDayRepeating && !task.Task.IsWeekRepeating)
            HC2_RequiredTasksMustBeScheduledConstraint += -coefficient;

        SC1_TotalPriorityConstraint += coefficient * task.Task.Priority;

        UpdateWeekMinOpt(task.Task, day.Day, add);

        var prevDayDifficulty = day.TotalDifficulty - (coefficient * task.Task.Difficulty);
        TotalDaysDifficultySum += day.TotalDifficulty - prevDayDifficulty;

        //if loops would be a hot path we could do batches for such constraints (that require full loop)
        //also SC7 could be done without loop if it would REALLY be a hot path
        var averageDifficulty = (double)TotalDaysDifficultySum / PlanningDays.Length;
        SC7_TotalDifficultyDifference = (int)Math.Ceiling(PlanningDays.Sum(d => Math.Pow((d.TotalDifficulty - averageDifficulty), 2)));
    }

    private void UpdateWeekMinOpt(Task task, Day day, bool add)
    {
        if (!task.IsWeekRepeating)
            return;

        var weekRepeatingTasks = WeekRepeatingTasksCount[day.WeekNumber];

        var prevCount = weekRepeatingTasks[task.Id];
        var newCount = prevCount + (add ? 1 : -1);
        weekRepeatingTasks[task.Id] = newCount;

        var minWeekCount = task.Repeating!.MinWeekCount;
        var optWeekCount = task.Repeating!.OptWeekCount;
        if (prevCount < minWeekCount && newCount >= minWeekCount
            || prevCount > optWeekCount && newCount <= optWeekCount)
        {
            HC6_RespectWeekMinOptCountConstraint--;
        }
        else if (prevCount >= minWeekCount && newCount < minWeekCount
            || prevCount <= optWeekCount && newCount > optWeekCount)
        {
            HC6_RespectWeekMinOptCountConstraint++;
        }

        SC5_MinimizeDifferenceFromWeekOptConstraint -= optWeekCount - prevCount;
        SC5_MinimizeDifferenceFromWeekOptConstraint += optWeekCount - newCount;
    }
}
