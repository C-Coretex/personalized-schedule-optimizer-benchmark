using Specialized.Optimizer.Models.Tasks;

namespace Specialized.Optimizer.Optimizer.Models.Domain;

internal partial record PlanningDay
{
    public int HC1_NoTaskOverlaps { get; private set; } = 0;

    public int HC7_RespectDayMinOptCountConstraint { get; private set; } = 0;

    public Dictionary<Guid, int> DayRepeatingTasksCount { get; private set; }

    public int TotalDifficulty { get; private set; } = 0;
    public int SC2_TotalDayDifficultyConstraint { get; private set; } = 0;

    //actually we want for both to either penalize or reward
    //TODO: maybe this could be separate objective
    public int SC3_DifficultTaskSchedulingConstraint { get; private set; } = 0;

    public int SC4_TypeWeightsConstraint { get; private set; } = 0;

    public int SC6_MinimizeDifferenceFromDayOptConstraint { get; private set; } = 0;

    private void InitConstraintValues(PlanningDomain domain)
    {
        TotalDifficulty = Day.FixedTasks.Sum(t => t.Task.Difficulty);
        SC2_TotalDayDifficultyConstraint = Math.Max(0, TotalDifficulty - Day.DifficultyCapacity);

        SC4_TypeWeightsConstraint = Day.FixedTasks.Sum(ft => 
            Day.TypeWeights.Where(tw => ft.Task.Types.Contains(tw.Key)).Select(tw => tw.Value).Sum());

        foreach (var repeatingTask in domain.Domain.Tasks.Where(t => t.Repeating is not null))
        {
            HC7_RespectDayMinOptCountConstraint += repeatingTask.Repeating!.MinDayCount ?? 0;
            SC6_MinimizeDifferenceFromDayOptConstraint += repeatingTask.Repeating!.OptDayCount;
        }
    }

    private void UpdateConstraintValues(Task task, bool add)
    {
        UpdateDayMinOpt(task, add);

        var coefficient = add ? 1 : -1;

        TotalDifficulty += coefficient * task.Difficulty;
        SC2_TotalDayDifficultyConstraint = Math.Max(0, TotalDifficulty - Day.DifficultyCapacity);

        UpdateScheduledTaskBasedConstraints();

        var weight = task.TypeWeights[Day.Date];
        SC4_TypeWeightsConstraint += coefficient * weight;
    }

    private void UpdateDayMinOpt(Task task, bool add)
    {
        if (task.Repeating is null)
            return;

        var coefficient = add ? 1 : -1;

        var prevCount = DayRepeatingTasksCount[task.Id];
        var newCount = prevCount + coefficient;
        DayRepeatingTasksCount[task.Id] = newCount;

        var minDayCount = task.Repeating!.MinDayCount ?? 0;
        var optDayCount = task.Repeating!.OptDayCount;
        if (prevCount < minDayCount || newCount < minDayCount)
            HC7_RespectDayMinOptCountConstraint -= coefficient;
        if (newCount > optDayCount || prevCount > optDayCount)
            HC7_RespectDayMinOptCountConstraint += coefficient;

        SC6_MinimizeDifferenceFromDayOptConstraint -= optDayCount - prevCount;
        SC6_MinimizeDifferenceFromDayOptConstraint += optDayCount - newCount;
    }

    private void UpdateScheduledTaskBasedConstraints()
    {
        HC1_NoTaskOverlaps = 0;
        SC3_DifficultTaskSchedulingConstraint = 0;

        var i = -1;
        var j = -1;
        TimeOnly? previousDifficultTaskEnd = null;
        while(i < ScheduledTasks.Count - 1 || j < Day.DifficultFixedTasks.Length - 1)
        {
            if (j < Day.DifficultFixedTasks.Length - 1
                && (i >= ScheduledTasks.Count - 1 || Day.DifficultFixedTasks[j + 1].Start < ScheduledTasks[i + 1].Start))
            {
                j++;

                if (previousDifficultTaskEnd is not null)
                    SC3_DifficultTaskSchedulingConstraint += (int)(Day.DifficultFixedTasks[j].Start - previousDifficultTaskEnd.Value).TotalMinutes;

                previousDifficultTaskEnd = Day.DifficultFixedTasks[j].End;
            }
            else if(i < ScheduledTasks.Count - 1)
            {
                i++;

                if (i > 0 && ScheduledTasks[i - 1].End > ScheduledTasks[i].Start)
                    HC1_NoTaskOverlaps++;

                if (ScheduledTasks[i].Task.IsDifficult)
                {
                    if (previousDifficultTaskEnd is not null)
                        SC3_DifficultTaskSchedulingConstraint += (int)(ScheduledTasks[i].Start - previousDifficultTaskEnd.Value).TotalMinutes;

                    previousDifficultTaskEnd = ScheduledTasks[i].End;
                }
            }
        }
    }
}
