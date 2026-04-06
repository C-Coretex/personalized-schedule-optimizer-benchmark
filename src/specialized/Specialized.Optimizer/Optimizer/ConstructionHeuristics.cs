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

        //collect pool of tasks
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
            foreach (var task in dailyRepeatingTasks.Where(domain.AvailableTasksPool.ContainsKey))
            {
                var taskCount = day.DayRepeatingTasksCount[task.Id];
                for (var i = taskCount; i < task.Repeating!.MinDayCount; i++)
                {
                    day.AddScheduledTaskInTimeWindow(task, TimeOnly.MinValue);
                }
            }
        }

        //get repeating for each week + respect max count
        var weeklyRepeatingTasks = repeatingTasks.Where(t => t.Repeating!.MinWeekCount > 0).ToArray();
        var weekDays = domain.PlanningDays.GroupBy(pd => pd.Day.WeekNumber);

        foreach (var week in weekDays)
        {
            foreach (var task in weeklyRepeatingTasks.Where(domain.AvailableTasksPool.ContainsKey))
            {
                var taskCount = domain.WeekRepeatingTasksCount[week.Key][task.Id];
                for (var i = taskCount; i < task.Repeating!.MinWeekCount; i++)
                {
                    var possibleDays = week.Where(d => d.DayRepeatingTasksCount[task.Id] < task.Repeating!.OptDayCount).ToArray();
                    var actualFreeTimeWindows = domain.GetActualFreeTimeWindowsFor(task, random, possibleDays: possibleDays);
                    
                    if (actualFreeTimeWindows.Length == 0)
                        continue;

                    var randomTimeWindow = actualFreeTimeWindows.RandomElement(random);
                    randomTimeWindow.Day.AddScheduledTask(task, randomTimeWindow.TimeWindow.Start);
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
