using Specialized.Optimizer.Models.Enums;
using System.Collections.Frozen;

namespace Specialized.Optimizer.Optimizer.Models.Domain;

internal record PlanningDay
{
    public PlanningDay(Day day, PlanningDomain domain)
    {
        Day = day;
        Domain = domain;

        DayRepeatingTasksCount  = domain.Domain.Tasks.Where(t => t.IsDayRepeating).ToDictionary(wt => wt.Id, _ => 0);
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

    #region Constraint Values

    public int HC1_NoTaskOverlaps { get; set; } = 0;

    public int HC7_RespectDayMinOptCountConstraint { get; set; } = 0;

    public Dictionary<Guid, int> DayRepeatingTasksCount { get; set; }

    public int TotalDifficulty { get; set; } = 0;
    public int SC2_TotalDayDifficultyConstraint { get; set; } = 0;

    public int SC3_DifficultTaskSchedulingConstraint { get; set; } = 0;

    public int SC4_TypeWeightsConstraint { get; set; } = 0;

    public int SC6_MinimizeDifferenceFromDayOptConstraint { get; set; } = 0;

    #endregion

    public PlanningDay GetSnapshot(PlanningDomain domain)
    {
        return this with
        {
            Domain = domain,
            _scheduledTasks = new(_scheduledTasks),
            DayRepeatingTasksCount = new(DayRepeatingTasksCount)
        };
    }

    public void AddScheduledTask(Task task, TimeOnly start)
    {
        var scheduledTask = new ScheduledTask()
        {
            Task = task,
            Start = start,
            End = start.AddMinutes(task.Duration)
        };
        //if start time is the same - replace the old task
        _scheduledTasks[start] = scheduledTask;
        _actualTimeWindows = null;

        UpdateConstraintValues(task, add: true);
        Domain.OnTaskAdded(scheduledTask, this);
    }

    /// <returns>True if added</returns>
    public bool AddScheduledTaskInTimeWindow(Task task, TimeOnly from, TimeOnly? to = null, bool stopIfUnfeasible = false)
    {
        //TODO: cache if will be hot path
        var actualTimeWindow = ScheduledTask.GetActualTimeWindowsForDay(this, task, from, to).FirstOrDefault();
        if(stopIfUnfeasible && actualTimeWindow == default)
            return false;

        var start = actualTimeWindow != default ? actualTimeWindow.Start : from;
        AddScheduledTask(task, start);

        return true;
    }

    public void RemoveScheduledTask(ScheduledTask task)
    {
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

    private void UpdateConstraintValues(Task task, bool add)
    {
        UpdateDayMinOpt(task, add);

        var coefficient = add ? 1 : -1;

        TotalDifficulty += coefficient * task.Difficulty;
        SC2_TotalDayDifficultyConstraint = Day.DifficultyCapacity - task.Difficulty;

        UpdateScheduledTaskBasedConstraints();

        var weight = task.TypeWeights[Day.Date];
        SC4_TypeWeightsConstraint += coefficient * weight;
    }

    private void UpdateDayMinOpt(Task task, bool add)
    {
        if (!task.IsDayRepeating)
            return;

        var prevCount = DayRepeatingTasksCount[task.Id];
        var newCount = prevCount + (add ? 1 : -1);
        DayRepeatingTasksCount[task.Id] = newCount;

        var minDayCount = task.Repeating!.MinDayCount;
        var optDayCount = task.Repeating!.OptDayCount;
        if (prevCount < minDayCount && newCount >= minDayCount
            || prevCount > optDayCount && newCount <= optDayCount)
        {
            HC7_RespectDayMinOptCountConstraint--;
        }
        else if (prevCount >= minDayCount && newCount < minDayCount
            || prevCount <= optDayCount && newCount > optDayCount)
        {
            HC7_RespectDayMinOptCountConstraint++;
        }

        SC6_MinimizeDifferenceFromDayOptConstraint -= optDayCount - prevCount;
        SC6_MinimizeDifferenceFromDayOptConstraint += optDayCount - newCount;
    }

    private void UpdateScheduledTaskBasedConstraints()
    {
        HC1_NoTaskOverlaps = 0;
        SC3_DifficultTaskSchedulingConstraint = 0;
        if (ScheduledTasks.Count == 0)
            return;

        var previousRequiredTask = ScheduledTasks[0].Task.IsRequired ? (ScheduledTask?)ScheduledTasks[0] : null;
        for(var i = 1; i < ScheduledTasks.Count; i++)
        {
            if (ScheduledTasks[i - 1].End > ScheduledTasks[i].Start)
                HC1_NoTaskOverlaps++;

            if(ScheduledTasks[i].Task.IsRequired)
            {
                if (previousRequiredTask is not null)
                    SC3_DifficultTaskSchedulingConstraint += (int)(ScheduledTasks[i].Start - previousRequiredTask.Value.End).TotalMinutes;

                previousRequiredTask = ScheduledTasks[i];
            }
        }

        if (Domain.Domain.DifficultTaskSchedulingStrategy == DifficultTaskSchedulingStrategy.Even)
            SC3_DifficultTaskSchedulingConstraint = -SC3_DifficultTaskSchedulingConstraint;
    }
}
