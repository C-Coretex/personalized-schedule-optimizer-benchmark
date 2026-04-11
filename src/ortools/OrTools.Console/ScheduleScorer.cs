using System.Globalization;
using OrTools.Optimizer.Models;
using OrTools.Optimizer.Models.Enums;
using OrTools.Optimizer.Models.Tasks;

namespace OrTools.Console;

public record ConstraintScore(string Id, string Name, int Score);

public record ScheduleScore(
    List<ConstraintScore> HardConstraints,
    List<ConstraintScore> SoftConstraints,
    int TotalHard,
    int TotalSoft
);

internal static class ScheduleScorer
{
    private record ScheduledTask(DateTime Start, DateTime End, TaskBase Task);

    public static ScheduleScore Calculate(GenerateScheduleRequest request, GenerateScheduleResponse response)
    {
        var byId = request.FixedTasks.Cast<TaskBase>()
            .Concat(request.DynamicTasks)
            .ToDictionary(t => t.Id);

        var allTimeline = response.TasksTimeline
            .Select(tt => byId.TryGetValue(tt.Id, out var task)
                ? new ScheduledTask(tt.StartTime, tt.EndTime, task)
                : null)
            .Where(t => t is not null).Select(t => t!)
            .OrderBy(t => t.Start).ToArray();

        var dynamicTimeline = allTimeline
            .Where(t => t.Task is DynamicTask)
            .Select(t => new ScheduledTask(t.Start, t.End, (DynamicTask)t.Task))
            .ToArray();

        var hc1 = HC1(allTimeline);
        var hc2 = HC2(request, dynamicTimeline);
        var hc3 = HC3(dynamicTimeline);
        var hc4 = HC4(dynamicTimeline);
        var hc5 = HC5(request, dynamicTimeline);
        var hc6 = HC6(request, dynamicTimeline);
        var hc7 = HC7(request, dynamicTimeline);
        var hc8 = HC8(request, dynamicTimeline);
        var hc9 = HC9(dynamicTimeline);

        var sc1 = SC1(dynamicTimeline);
        var sc2 = SC2(request, allTimeline);
        var sc3 = SC3(request, allTimeline);
        var sc4 = SC4(request, allTimeline);
        var sc5 = SC5(request, dynamicTimeline);
        var sc6 = SC6(request, dynamicTimeline);
        var sc7 = SC7(request, allTimeline);

        var totalHard = hc1 + hc2 + (int)Math.Ceiling(hc3 / 60d) + (int)Math.Ceiling(hc4 / 60d)
            + hc5 + hc6 + hc7 + (int)Math.Ceiling(hc8 / 60d) + hc9;
        var totalSoft = 100 * sc1 + 500 * sc2 + sc3 + sc4 + 50 * sc5 + 50 * sc6 + sc7;

        return new ScheduleScore(
            [
                new("HC1", "No task time overlaps", hc1),
                new("HC2", "All required tasks scheduled", hc2),
                new("HC3", "Tasks within time windows (min)", hc3),
                new("HC4", "Tasks before deadline (min)", hc4),
                new("HC5", "Tasks within category windows", hc5),
                new("HC6", "Week-repeating min/opt count", hc6),
                new("HC7", "Day-repeating min/opt count", hc7),
                new("HC8", "Tasks within planning horizon (min)", hc8),
                new("HC9", "Non-repeating tasks at most once", hc9),
            ],
            [
                new("SC1", "Maximize total priority", sc1),
                new("SC2", "Difficulty within daily capacity", sc2),
                new("SC3", "Difficult task strategy (gap sum)", sc3),
                new("SC4", "Task type preferences", sc4),
                new("SC5", "Week-repeating optimal count", sc5),
                new("SC6", "Day-repeating optimal count", sc6),
                new("SC7", "Difficulty balance between days", sc7),
            ],
            totalHard,
            totalSoft
        );
    }

    // HC1: no task time overlaps
    private static int HC1(ScheduledTask[] timeline)
    {
        int score = 0;
        for (int i = 0; i < timeline.Length - 1; i++)
            if (timeline[i].End > timeline[i + 1].Start)
                score++;
        return score;
    }

    // HC2: all required non-repeating tasks must be scheduled
    private static int HC2(GenerateScheduleRequest request, ScheduledTask[] dynamicTimeline)
    {
        var scheduledIds = dynamicTimeline.Select(t => t.Task.Id).ToHashSet();
        return request.DynamicTasks
            .Count(t => t.IsRequired && t.Repeating is null && !scheduledIds.Contains(t.Id));
    }

    // HC3: tasks scheduled within their time windows (returns total minutes of violation)
    private static int HC3(ScheduledTask[] dynamicTimeline)
    {
        return (int)dynamicTimeline.Select(t =>
        {
            var dt = (DynamicTask)t.Task;
            double before = dt.WindowStart is { } ws
                ? Math.Max(0, ws.ToTimeSpan().TotalMinutes - t.Start.TimeOfDay.TotalMinutes)
                : 0;
            double after = dt.WindowEnd is { } we
                ? Math.Max(0, t.End.TimeOfDay.TotalMinutes - we.ToTimeSpan().TotalMinutes)
                : 0;
            return before + after;
        }).Sum();
    }

    // HC4: tasks done before deadline (returns total minutes past deadline)
    private static int HC4(ScheduledTask[] dynamicTimeline)
    {
        return (int)dynamicTimeline
            .Where(t => ((DynamicTask)t.Task).Deadline is not null)
            .Sum(t => Math.Max(0, (t.End - ((DynamicTask)t.Task).Deadline!.Value).TotalMinutes));
    }

    // HC5: tasks not outside their category windows
    private static int HC5(GenerateScheduleRequest request, ScheduledTask[] dynamicTimeline)
    {
        if (request.CategoryWindows.Count == 0) return 0;

        var windowsByCategory = request.CategoryWindows
            .GroupBy(cw => cw.Category)
            .ToDictionary(g => g.Key, g => g.ToArray());

        return dynamicTimeline.Count(t =>
        {
            var dt = (DynamicTask)t.Task;
            if (dt.Categories.Count == 0) return false;
            return dt.Categories.All(c =>
            {
                if (!windowsByCategory.TryGetValue(c, out var windows)) return false;
                return !windows.Any(w => t.Start >= w.StartDateTime && t.End <= w.EndDateTime);
            });
        });
    }

    // HC6: week-repeating tasks meet min/opt per week
    private static int HC6(GenerateScheduleRequest request, ScheduledTask[] dynamicTimeline)
    {
        var weeks = GetWeeks(request.PlanningHorizon).ToArray();
        var scheduledRepeating = dynamicTimeline.Where(t => ((DynamicTask)t.Task).Repeating is not null).ToArray();
        var scheduledRepeatingTasks = scheduledRepeating.Select(t => t.Task).ToHashSet();
        var nonAdded = request.DynamicTasks
            .Where(t => t.Repeating is not null && !scheduledRepeatingTasks.Contains(t))
            .ToArray();

        var fromScheduled = scheduledRepeating
            .GroupBy(t => t.Task)
            .Sum(g =>
            {
                var rep = ((DynamicTask)g.Key).Repeating!;
                int sum = 0;
                foreach (var (start, end) in weeks)
                {
                    int count = g.Count(t =>
                        t.Start.Date >= start.ToDateTime(TimeOnly.MinValue) &&
                        t.Start.Date <= end.ToDateTime(TimeOnly.MaxValue));
                    sum += Math.Max(0, (rep.MinWeekCount ?? 0) - count) + Math.Max(0, count - rep.OptWeekCount);
                }
                return sum;
            });

        var fromNonAdded = nonAdded.Sum(t => (t.Repeating!.MinWeekCount ?? 0) * weeks.Length);
        return fromScheduled + fromNonAdded;
    }

    // HC7: day-repeating tasks meet min/opt per day
    private static int HC7(GenerateScheduleRequest request, ScheduledTask[] dynamicTimeline)
    {
        var days = GetDays(request.PlanningHorizon).ToArray();
        var scheduledRepeating = dynamicTimeline.Where(t => ((DynamicTask)t.Task).Repeating is not null).ToArray();
        var scheduledRepeatingTasks = scheduledRepeating.Select(t => t.Task).ToHashSet();
        var nonAdded = request.DynamicTasks
            .Where(t => t.Repeating is not null && !scheduledRepeatingTasks.Contains(t))
            .ToArray();

        var fromScheduled = scheduledRepeating
            .GroupBy(t => t.Task)
            .Sum(g =>
            {
                var rep = ((DynamicTask)g.Key).Repeating!;
                int sum = 0;
                foreach (var day in days)
                {
                    int count = g.Count(t => DateOnly.FromDateTime(t.Start.Date) == day);
                    sum += Math.Max(0, (rep.MinDayCount ?? 0) - count) + Math.Max(0, count - rep.OptDayCount);
                }
                return sum;
            });

        var fromNonAdded = nonAdded.Sum(t => (t.Repeating!.MinDayCount ?? 0) * days.Length);
        return fromScheduled + fromNonAdded;
    }

    // HC8: tasks within planning horizon (returns total minutes outside)
    private static int HC8(GenerateScheduleRequest request, ScheduledTask[] dynamicTimeline)
    {
        var start = request.PlanningHorizon.StartDate.ToDateTime(TimeOnly.MinValue);
        var end = request.PlanningHorizon.EndDate.ToDateTime(TimeOnly.MaxValue);
        return (int)dynamicTimeline.Sum(t =>
            Math.Max(0, (start - t.Start).TotalMinutes) + Math.Max(0, (t.End - end).TotalMinutes));
    }

    // HC9: non-repeating tasks appear at most once
    private static int HC9(ScheduledTask[] dynamicTimeline)
    {
        return dynamicTimeline
            .Where(t => ((DynamicTask)t.Task).Repeating is null)
            .GroupBy(t => t.Task.Id)
            .Count(g => g.Count() > 1);
    }

    // SC1: maximize total priority (-1 * sum(6 - priority))
    private static int SC1(ScheduledTask[] dynamicTimeline)
    {
        return -1 * dynamicTimeline.Sum(t => 6 - t.Task.Priority);
    }

    // SC2: minimize total difficulty above daily capacity
    private static int SC2(GenerateScheduleRequest request, ScheduledTask[] allTimeline)
    {
        return request.DifficultyCapacities.Sum(dc =>
        {
            var dayStart = dc.Date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dc.Date.ToDateTime(TimeOnly.MaxValue);
            var dayDifficulty = allTimeline
                .Where(t => t.Start >= dayStart && t.End <= dayEnd)
                .Sum(t => t.Task.Difficulty);
            return Math.Max(0, dayDifficulty - dc.Capacity);
        });
    }

    // SC3: difficult task scheduling strategy
    private static int SC3(GenerateScheduleRequest request, ScheduledTask[] allTimeline)
    {
        var difficultByDay = allTimeline
            .Where(t => t.Task.Difficulty >= 6)
            .GroupBy(t => t.Start.Date)
            .Select(g => g.OrderBy(t => t.Start).ToArray());

        int sum = 0;
        foreach (var tasks in difficultByDay)
            for (int i = 0; i < tasks.Length - 1; i++)
                sum += (int)(tasks[i + 1].Start - tasks[i].End).TotalMinutes;

        int coeff = request.DifficultTaskSchedulingStrategy == DifficultTaskSchedulingStrategy.Cluster ? 1 : -1;
        return coeff * sum;
    }

    // SC4: maximize task type preferences
    private static int SC4(GenerateScheduleRequest request, ScheduledTask[] allTimeline)
    {
        var prefsByDay = request.TaskTypePreferences.ToDictionary(p => p.Date, p => p.Preferences);
        return -1 * allTimeline.Sum(t =>
        {
            if (!prefsByDay.TryGetValue(DateOnly.FromDateTime(t.Start.Date), out var prefs)) return 0;
            return prefs.Where(p => t.Task.Types.Contains(p.Type)).Sum(p => p.Weight);
        });
    }

    // SC5: minimize under-scheduling of week-repeating tasks vs optimal
    private static int SC5(GenerateScheduleRequest request, ScheduledTask[] dynamicTimeline)
    {
        var weeks = GetWeeks(request.PlanningHorizon).ToArray();
        var scheduledRepeating = dynamicTimeline.Where(t => ((DynamicTask)t.Task).Repeating is not null).ToArray();
        var scheduledRepeatingTasks = scheduledRepeating.Select(t => t.Task).ToHashSet();
        var nonAdded = request.DynamicTasks
            .Where(t => t.Repeating is not null && !scheduledRepeatingTasks.Contains(t))
            .ToArray();

        var fromScheduled = scheduledRepeating
            .GroupBy(t => t.Task)
            .Sum(g =>
            {
                var rep = ((DynamicTask)g.Key).Repeating!;
                int sum = 0;
                foreach (var (start, end) in weeks)
                {
                    int count = g.Count(t =>
                        t.Start.Date >= start.ToDateTime(TimeOnly.MinValue) &&
                        t.Start.Date <= end.ToDateTime(TimeOnly.MaxValue));
                    sum += Math.Max(0, rep.OptWeekCount - count);
                }
                return sum;
            });

        var fromNonAdded = nonAdded.Sum(t => t.Repeating!.OptWeekCount * weeks.Length);
        return fromScheduled + fromNonAdded;
    }

    // SC6: minimize under-scheduling of day-repeating tasks vs optimal
    private static int SC6(GenerateScheduleRequest request, ScheduledTask[] dynamicTimeline)
    {
        var days = GetDays(request.PlanningHorizon).ToArray();
        var scheduledRepeating = dynamicTimeline.Where(t => ((DynamicTask)t.Task).Repeating is not null).ToArray();
        var scheduledRepeatingTasks = scheduledRepeating.Select(t => t.Task).ToHashSet();
        var nonAdded = request.DynamicTasks
            .Where(t => t.Repeating is not null && !scheduledRepeatingTasks.Contains(t))
            .ToArray();

        var fromScheduled = scheduledRepeating
            .GroupBy(t => t.Task)
            .Sum(g =>
            {
                var rep = ((DynamicTask)g.Key).Repeating!;
                int sum = 0;
                foreach (var day in days)
                {
                    int count = g.Count(t => DateOnly.FromDateTime(t.Start.Date) == day);
                    sum += Math.Max(0, rep.OptDayCount - count);
                }
                return sum;
            });

        var fromNonAdded = nonAdded.Sum(t => t.Repeating!.OptDayCount * days.Length);
        return fromScheduled + fromNonAdded;
    }

    // SC7: minimize difficulty imbalance between days
    private static int SC7(GenerateScheduleRequest request, ScheduledTask[] allTimeline)
    {
        var dayDifficulties = GetDays(request.PlanningHorizon)
            .Select(d => allTimeline.Where(t => DateOnly.FromDateTime(t.Start.Date) == d).Sum(t => t.Task.Difficulty))
            .ToArray();
        var avg = dayDifficulties.Average();
        return (int)Math.Ceiling(dayDifficulties.Sum(d => Math.Pow(d - avg, 2)));
    }

    private static IEnumerable<DateOnly> GetDays(OrTools.Optimizer.Models.Payload.PlanningHorizon horizon)
    {
        var current = horizon.StartDate;
        while (current <= horizon.EndDate)
        {
            yield return current;
            current = current.AddDays(1);
        }
    }

    private static IEnumerable<(DateOnly Start, DateOnly End)> GetWeeks(OrTools.Optimizer.Models.Payload.PlanningHorizon horizon)
    {
        var calendar = CultureInfo.CurrentCulture.Calendar;
        var weekStart = horizon.StartDate;
        int? currentWeek = null;
        foreach (var day in GetDays(horizon))
        {
            int weekNum = calendar.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue), CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            currentWeek ??= weekNum;
            if (weekNum != currentWeek)
            {
                yield return (weekStart, day.AddDays(-1));
                weekStart = day;
                currentWeek = weekNum;
            }
        }
        if (currentWeek.HasValue)
            yield return (weekStart, horizon.EndDate);
    }
}
