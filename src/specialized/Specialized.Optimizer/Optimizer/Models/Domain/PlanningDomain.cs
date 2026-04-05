namespace Specialized.Optimizer.Optimizer.Models.Domain;

//separate from Domain to optimize snapshots
internal partial record PlanningDomain
{
    public PlanningDomain(Domain domain)
    {
        Domain = domain;

        var weekTasks = domain.Tasks.Where(t => t.IsWeekRepeating).ToArray();
        WeekRepeatingTasksCount = domain.Days.GroupBy(d => d.WeekNumber).ToDictionary(g => g.Key, g =>
            weekTasks.ToDictionary(wt => wt.Id, _ => 0));

        PlanningDays = [.. domain.Days.Select(day => new PlanningDay(day, this))];

        InitConstraintValues(domain);
    }

    public void OnTaskAdded(ScheduledTask task, PlanningDay day)
        => UpdateConstraintValues(task, day, add: true);

    public void OnTaskRemoved(ScheduledTask task, PlanningDay day)
        => UpdateConstraintValues(task, day, add: false);

    public Domain Domain { get; init; }

    public void Swap(ScheduledTask task, PlanningDay taskDay, PlanningDay targetDay, TimeOnly start)
    {
        var end = start.AddMinutes(task.Task.Duration);
        taskDay.RemoveScheduledTask(task);

        var tasksToRemove = targetDay.ScheduledTasks.Where(t => t.Start >= start && t.Start < end || t.End > start && t.End <= end).ToArray();
        foreach (var taskToRemove in tasksToRemove)
        {
            targetDay.RemoveScheduledTask(taskToRemove);
            taskDay.AddScheduledTaskInTimeWindow(taskToRemove.Task, TimeOnly.MinValue, stopIfUnfeasible: true);
        }

        targetDay.AddScheduledTask(task.Task, start);
    }

    public ScheduledTask[] Replace(Task task, PlanningDay targetDay, TimeOnly start)
    {
        var end = start.AddMinutes(task.Duration);

        var tasksToRemove = targetDay.ScheduledTasks.Where(t => (t.Start >= start && t.Start < end) || (t.End > start && t.End <= end)).ToArray();
        foreach (var taskToRemove in tasksToRemove)
            targetDay.RemoveScheduledTask(taskToRemove);

        targetDay.AddScheduledTask(task, start);

        return tasksToRemove;
    }

    public ScheduledTask[] Replace(ScheduledTask task, PlanningDay taskDay, PlanningDay targetDay, TimeOnly start)
    {
        var end = start.AddMinutes(task.Task.Duration);
        taskDay.RemoveScheduledTask(task);

        var tasksToRemove = targetDay.ScheduledTasks.Where(t => (t.Start >= start && t.Start < end) || (t.End > start && t.End <= end)).ToArray();
        foreach (var taskToRemove in tasksToRemove)
            targetDay.RemoveScheduledTask(taskToRemove);

        targetDay.AddScheduledTask(task.Task, start);

        return tasksToRemove;
    }

    //actual planning entity
    public PlanningDay[] PlanningDays { get; private set; } = [];

    public PlanningDomain GetSnapshot()
    {
        var snapshot = this with
        {
            AvailableTasksPool = new(AvailableTasksPool),
            WeekRepeatingTasksCount = WeekRepeatingTasksCount.ToDictionary(
                kvp => kvp.Key,
                kvp => new Dictionary<Guid, int>(kvp.Value))
        };
        snapshot.PlanningDays = [.. PlanningDays.Select(pd => pd.GetSnapshot(snapshot))];

        return snapshot;
    }

    public (CategoryTimeWindow TimeWindow, PlanningDay Day)[] GetActualFreeTimeWindowsFor(Task task, bool useDefaultIfNoActuallyFree = true)
    {
        var entries = PlanningDays.SelectMany(d => ScheduledTask.GetActualTimeWindowsForDay(d, task).Select(t => (t, d))).ToArray();
        if (!useDefaultIfNoActuallyFree || entries.Length > 0)
            return entries;

        return PlanningDays.SelectMany(d =>
        {
            if (!task.FreeTimeWindowsByDate.TryGetValue(d.Day.Date, out var freeTimeWindows))
                return [];
            return freeTimeWindows.Select(tw => (tw, d));
        }).ToArray();
    }
}
