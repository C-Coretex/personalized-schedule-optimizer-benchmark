namespace Specialized.Optimizer.Optimizer.Models.Domain;

internal readonly record struct ScheduledTask
{
    public Task Task { get; init; }
    public TimeOnly Start { get; init; }
    public TimeOnly End { get; init; }

    public static IEnumerable<CategoryTimeWindow> GetActualTimeWindowsForDay(PlanningDay day, Task task, TimeOnly? from = null, TimeOnly? to = null)
    {
        if (!task.FreeTimeWindowsByDate.TryGetValue(day.Day.Date, out var taskFreeTimeWindowsArray))
            taskFreeTimeWindowsArray = [];
        var taskFreeTimeWindows = taskFreeTimeWindowsArray.AsEnumerable();

        if (from is not null)
            taskFreeTimeWindows = taskFreeTimeWindows.Where(ftw => ftw.End > from)
                .Select(ftw => from > ftw.Start ? ftw with { Start = from.Value } : ftw);
        if (to is not null)
            taskFreeTimeWindows = taskFreeTimeWindows.Where(ftw => ftw.Start < to)
                .Select(ftw => to < ftw.End ? ftw with { End = to.Value } : ftw);

        var taskFreeTimeWindowEnumerator = taskFreeTimeWindows.GetEnumerator();
        var isTaskFreeTimeWindowPresent = taskFreeTimeWindowEnumerator.MoveNext();
        var currentTaskFreeTimeWindow = isTaskFreeTimeWindowPresent ? taskFreeTimeWindowEnumerator.Current : default;

        foreach (var actualTimeWindow in day.ActualTimeWindows)
        {
            if (!isTaskFreeTimeWindowPresent)
                break;
            if (currentTaskFreeTimeWindow.Start >= actualTimeWindow.End)
                continue;

            while (isTaskFreeTimeWindowPresent && currentTaskFreeTimeWindow.End <= actualTimeWindow.End)
            {
                var timeWindow = currentTaskFreeTimeWindow with
                {
                    Start = currentTaskFreeTimeWindow.Start < actualTimeWindow.Start ? actualTimeWindow.Start : currentTaskFreeTimeWindow.Start,
                    End = currentTaskFreeTimeWindow.End > actualTimeWindow.End ? actualTimeWindow.End : currentTaskFreeTimeWindow.End
                };

                if (timeWindow.End - timeWindow.Start >= TimeSpan.FromMinutes(task.Duration))
                    yield return timeWindow;

                isTaskFreeTimeWindowPresent = taskFreeTimeWindowEnumerator.MoveNext();
                currentTaskFreeTimeWindow = isTaskFreeTimeWindowPresent ? taskFreeTimeWindowEnumerator.Current : default;
            }
        }
    }
}
