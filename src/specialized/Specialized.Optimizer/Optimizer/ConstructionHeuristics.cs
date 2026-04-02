using Specialized.Optimizer.Helpers;
using Specialized.Optimizer.Optimizer.Models.Domain;

namespace Specialized.Optimizer.Optimizer;

internal static class ConstructionHeuristics
{
    public static PlanningDomain Construct(PlanningDomain domain, Random random)
    {
        domain = domain.GetSnapshot();

        ConstructRepeatingTasks(domain, random);

        //collect pool of tasks
        ConstructNonRepeatingTasks(domain, random);


        return domain;
    }

    private static void ConstructRepeatingTasks(PlanningDomain domain, Random random)
    {
        var repeatingTasks = domain.Domain.Tasks.Where(t => t.Repeating != null)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.FreeTimeWindows.Sum(ftw => (ftw.End - ftw.Start).Minutes))
            .ToArray();

        //get repeating for each day
        var dailyRepeatingTasks = repeatingTasks.Where(t => t.Repeating!.MinDayCount != null).ToArray();
        foreach (var day in domain.PlanningDays)
        {
            foreach (var task in dailyRepeatingTasks)
            {
                var freeTimeWindows = task.FreeTimeWindows.Where(tw => tw.Day == day.Day).ToArray();
                if (freeTimeWindows.Length == 0)
                    continue;

                for (var i = 0; i < task.Repeating!.MinDayCount; i++)
                {
                    var randomTimeWindow = freeTimeWindows.RandomElement(random);
                    day.AddScheduledTaskInTimeWindow(task, randomTimeWindow.Start);
                }
            }
        }

        //get repeating for each week + respect max count
        var weeklyRepeatingTasks = repeatingTasks.Where(t => t.Repeating!.MinWeekCount != null).ToArray();
        var weekDays = domain.PlanningDays.GroupBy(pd => pd.Day.WeekNumber);

        foreach (var days in weekDays.Select(g => g.ToArray()))
        {
            foreach (var task in weeklyRepeatingTasks)
            {
                for (var i = 0; i < task.Repeating!.MinWeekCount; i++)
                {
                    var possibleDays = days.Where(d => d.ScheduledTasks.Count(st => st.Task == task) < task.Repeating!.OptDayCount).ToArray();
                    var freeTimeWindows = task.FreeTimeWindows.Where(tw => possibleDays.Any(d => d.Day == tw.Day)).ToArray();
                    if (freeTimeWindows.Length == 0)
                        continue;

                    var randomTimeWindow = freeTimeWindows.RandomElement(random);
                    days.First(d => d.Day == randomTimeWindow.Day).AddScheduledTaskInTimeWindow(task, randomTimeWindow.Start);
                }
            }
        }
    }

    private static void ConstructNonRepeatingTasks(PlanningDomain domain, Random random)
    {
        var repeatingTasks = domain.Domain.Tasks.Where(t => t.Repeating == null && t.IsRequired)
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
