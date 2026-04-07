namespace Specialized.Optimizer.Optimizer.Models.Domain;

internal partial record PlanningDay
{
    public PlanningDay(Day day, PlanningDomain domain)
    {
        Day = day;
        Domain = domain;

        DayRepeatingTasksCount  = domain.Domain.Tasks.OrderBy(t => t.Id)
            .Where(t => t.Repeating is not null).ToDictionary(wt => wt.Id, _ => 0);

        InitConstraintValues(domain);
    }

    public Day Day { get; init; }
    public PlanningDomain Domain { get; init; }

    private SortedList<TimeOnly, ScheduledTask> _scheduledTasks = new(8);
    //actual planning property
    //can contain unfeasible values (overlapping tasks)
    public IList<ScheduledTask> ScheduledTasks => _scheduledTasks.Values;

    //actual free time windows (possible-scheduled tasks)
    //return values ordered by start time
    //if we need to optimize - we can modify the value on tasks/adding/removing, not recalculate it from scratch
    private FreeTimeWindow[]? _actualTimeWindows;
    public IReadOnlyCollection<FreeTimeWindow> ActualTimeWindows => _actualTimeWindows ??= [.. GetActualTimeWindows()];

    public PlanningDay GetSnapshot(PlanningDomain domain)
    {
        return this with
        {
            Domain = domain,
            _scheduledTasks = new(_scheduledTasks),
            DayRepeatingTasksCount = new(DayRepeatingTasksCount)
        };
    }

    public bool AddScheduledTask(Task task, TimeOnly start, bool stopIfUnfeasible = false)
    {
        var scheduledTask = new ScheduledTask()
        {
            Task = task,
            Start = start,
            End = start.AddMinutes(task.Duration)
        };

        //infeasible
        if (!scheduledTask.Task.FreeTimeWindowsByDate.TryGetValue(Day.Date, out var freeTimeWindows))
            return false;
        if (!freeTimeWindows.Any(ftw => ftw.Start <= scheduledTask.Start && ftw.End >= scheduledTask.End))
            return false;

        while (!_scheduledTasks.TryAdd(start, scheduledTask))
        {
            start = start.AddMinutes(1);
            if (stopIfUnfeasible || (TimeOnly.MaxValue - start).TotalMinutes < task.Duration)
                return false;

            scheduledTask = scheduledTask with { Start = start, End = start.AddMinutes(task.Duration) };
        }
        _actualTimeWindows = null;

        UpdateConstraintValues(task, add: true);
        Domain.OnTaskAdded(scheduledTask, this);

        return true;
    }

    /// <returns>True if added</returns>
    public bool AddScheduledTaskInTimeWindow(Task task, TimeOnly from, TimeOnly? to = null, bool stopIfUnfeasible = false)
    {
        //TODO: cache if will be hot path
        var actualTimeWindow = ScheduledTask.GetActualTimeWindowsForDay(this, task, from, to).FirstOrDefault();
        if(stopIfUnfeasible && actualTimeWindow == null)
            return false;

        var start = actualTimeWindow != null ? actualTimeWindow.Start : from;
        if (!AddScheduledTask(task, start, stopIfUnfeasible))
            return false;

        return true;
    }

    public void RemoveScheduledTask(ScheduledTask task)
    {
        if (!_scheduledTasks.TryGetValue(task.Start, out var existing) || existing.Task.Id != task.Task.Id)
            return;

        _scheduledTasks.Remove(task.Start);

        _actualTimeWindows = null;

        UpdateConstraintValues(task.Task, add: false);
        Domain.OnTaskRemoved(task, this);
    }

    private IEnumerable<FreeTimeWindow> GetActualTimeWindows()
    {
        var scheduledTaskEnumerator = ScheduledTasks.GetEnumerator();
        var isScheduledTaskPresent = scheduledTaskEnumerator.MoveNext();
        var currentScheduledTask = isScheduledTaskPresent ? scheduledTaskEnumerator.Current : default;

        //already ordered by start time
        foreach (var possibleTimeWindow in Day.PossibleTimeWindows)
        {
            //no need to check possibleTimeWindow.Start > currentScheduledTask.Start, because everything is ordered
            //we presume that ScheduledTasks always have correct data (in possible time window)
            if (!isScheduledTaskPresent || currentScheduledTask.Start >= possibleTimeWindow.End)
            {
                yield return possibleTimeWindow;
                continue;
            }

            //if start < end then task is scheduled inside the current time window (no need to check Start times because the solution is feasible)
            var currentPossibleTimeWindow = possibleTimeWindow;
            while (isScheduledTaskPresent && currentScheduledTask.End <= currentPossibleTimeWindow.End)
            {
                (var entry1, currentPossibleTimeWindow) = currentPossibleTimeWindow.CutOut(currentScheduledTask.Start, currentScheduledTask.End);
                yield return entry1;
                isScheduledTaskPresent = scheduledTaskEnumerator.MoveNext();
                currentScheduledTask = isScheduledTaskPresent ? scheduledTaskEnumerator.Current : default;
            }
            yield return currentPossibleTimeWindow;
        }
    }
}
