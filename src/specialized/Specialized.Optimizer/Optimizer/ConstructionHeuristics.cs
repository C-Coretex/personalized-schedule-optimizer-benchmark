using Specialized.Optimizer.Helpers;
using Specialized.Optimizer.Optimizer.Models.Domain;

namespace Specialized.Optimizer.Optimizer;

internal static class ConstructionHeuristics
{
    public static PlanningDomain Construct(PlanningDomain domain, Random random, bool createSnapshot = true)
    {
        if(createSnapshot)
            domain = domain.GetSnapshot();

        ConstructRepeatingTasks(domain, random);

        ConstructNonRepeatingTasks(domain, random);

        return domain;
    }

    private static void ConstructRepeatingTasks(PlanningDomain domain, Random random)
    {
        var repeatingTasks = domain.AvailableTasksPool.Select(t => t.Key).Where(t => t.Repeating is not null)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.FreeTimeWindows.Sum(ftw => (ftw.End - ftw.Start).Minutes));

        //get repeating for each day
        var dailyRepeatingTasks = repeatingTasks.Where(t => t.Repeating!.MinDayCount > 0).ToArray();
        foreach (var day in domain.PlanningDays.ShuffleElements(random))
        {
            foreach (var task in dailyRepeatingTasks)
            {
                if (!domain.AvailableTasksPool.TryGetValue(task, out var tasksLeft))
                    continue;

                var taskCount = day.DayRepeatingTasksCount[task.Id];
                for (var i = taskCount; i < task.Repeating!.MinDayCount && tasksLeft > 0; i++)
                {
                    if(day.AddScheduledTaskInTimeWindow(task, TimeOnly.MinValue))
                        tasksLeft--;
                }
            }
        }

        //get repeating for each week + respect max count
        var weeklyRepeatingTasks = repeatingTasks.Where(t => t.Repeating!.MinWeekCount > 0).ToArray();
        var weekDays = domain.PlanningDays.GroupBy(pd => pd.Day.WeekNumber);

        foreach (var week in weekDays)
        {
            foreach (var task in weeklyRepeatingTasks)
            {
                if (!domain.AvailableTasksPool.TryGetValue(task, out var tasksLeft))
                    continue;

                var taskCount = domain.WeekRepeatingTasksCount[week.Key][task.Id];
                for (var i = taskCount; i < task.Repeating!.MinWeekCount && tasksLeft > 0; i++)
                {

                    var possibleDays = week.Where(d => d.DayRepeatingTasksCount[task.Id] < task.Repeating!.OptDayCount).ToArray();
                    var actualFreeTimeWindows = domain.GetActualFreeTimeWindowsFor(task, random, possibleDays: possibleDays);
                    
                    if (actualFreeTimeWindows.Length == 0)
                        continue;

                    var randomTimeWindow = actualFreeTimeWindows.RandomElement(random);
                    if(randomTimeWindow.Day.AddScheduledTask(task, randomTimeWindow.TimeWindow.Start))
                        tasksLeft--;
                }
            }
        }
    }

    private static void ConstructNonRepeatingTasks(PlanningDomain domain, Random random)
    {
        var repeatingTasks = domain.AvailableTasksPool.Select(t => t.Key).Where(t => t.Repeating == null && t.IsRequired)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.FreeTimeWindows.Sum(ftw => (ftw.End - ftw.Start).Minutes))
            .ToArray();

        //iterate through tasks and assign them to random free time window (if possible)
        //TODO: could be hot path
        foreach (var task in repeatingTasks)
        {
            var days = task.FreeTimeWindows.Select(ftw => ftw.Day).Distinct().ToArray().ShuffleElements(random);
            foreach (var day in days)
            {
                var added = domain.PlanningDays.First(d => d.Day == day)
                    .AddScheduledTaskInTimeWindow(task, TimeOnly.MinValue, stopIfUnfeasible: true);
                if (added)
                    break;
            }
        }
    }
}
