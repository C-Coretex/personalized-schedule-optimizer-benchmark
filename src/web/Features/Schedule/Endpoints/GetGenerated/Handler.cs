using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Web.Features.Schedule.Endpoints.GetGenerated.Models;
using Web.Features.Schedule.Models.Schedule;
using Web.Providers.Models;
using Web.Providers.Models.Enums;
using Web.Providers.Models.Tasks;
using Web.Providers.Schedule.Models.Tasks;

namespace Web.Features.Schedule.Endpoints.GetGenerated;

public class Handler(IHttpContextAccessor httpContextAccessor, IMemoryCache cache)
{
    public async Task<Response?> Handle(CancellationToken ct)
    {
        var session = httpContextAccessor.HttpContext?.Session;
        if (session is null) return null;

        await session.LoadAsync(ct);
        var scheduleIdsJson = session.GetString("schedule_data");
        if (string.IsNullOrEmpty(scheduleIdsJson)) return null;

        var jobs = JsonSerializer.Deserialize<List<ScheduleJobMetadata>>(scheduleIdsJson)!;
        var responseEntries = jobs.Select(meta =>
        {
            if (meta.Response is null && cache.TryGetValue($"job_result_{meta.Id}", out GeneratedSchedule? schedule))
                meta = meta with { Response = schedule };

            var scheduledDynamicIds = meta.Response?.TasksTimeline
                .Where(tt => meta.Request.DynamicTasks.Any(dt => dt.Id == tt.Id))
                .Select(tt => tt.Id)
                .ToHashSet() ?? [];
            var unscheduled = meta.Request.DynamicTasks
                .Where(t => !scheduledDynamicIds.Contains(t.Id))
                .ToList();

            return new ResponseEntry(CalculateJobScore(meta), meta, unscheduled);
        }).ToList();

        return new Response(responseEntries);
    }

    private ResponseScore? CalculateJobScore(ScheduleJobMetadata metadata)
    {
        if (metadata.Response is null)
            return null;

        var fixedTasksTimeline = metadata.Response.TasksTimeline
            .Select(tt => metadata.Request.FixedTasks.FirstOrDefault(ft => ft.Id == tt.Id) is { } ft 
                            ? new ScheduledTask<FixedTask>(tt.StartTime, tt.EndTime, ft) : null)
            .Where(t => t is not null).Select(t => t!).OrderBy(t => t.Start).ToArray();
        var dynamicTasksTimeline = metadata.Response.TasksTimeline
            .Select(tt => metadata.Request.DynamicTasks.FirstOrDefault(dt => dt.Id == tt.Id) is { } dt
                            ? new ScheduledTask<DynamicTask>(tt.StartTime, tt.EndTime, dt) : null)
            .Where(t => t is not null).Select(t => t!).OrderBy(t => t.Start).ToArray();
        var tasksTimeline = fixedTasksTimeline.Select(ft => ft.ToTaskBase()).Concat(dynamicTasksTimeline.Select(dt => dt.ToTaskBase()))
            .OrderBy(t => t.Start).ToArray();

        var hc1 = new ResponseConstraintScore("HC1", HC1(tasksTimeline));
        var hc2 = new ResponseConstraintScore("HC2", HC2(metadata.Request, dynamicTasksTimeline));
        var hc3 = new ResponseConstraintScore("HC3", HC3(dynamicTasksTimeline));
        var hc4 = new ResponseConstraintScore("HC4", HC4(dynamicTasksTimeline));
        var hc5 = new ResponseConstraintScore("HC5", HC5(metadata.Request, dynamicTasksTimeline));
        var hc6 = new ResponseConstraintScore("HC6", HC6(metadata.Request, dynamicTasksTimeline));
        var hc7 = new ResponseConstraintScore("HC7", HC7(metadata.Request, dynamicTasksTimeline));
        var hc8 = new ResponseConstraintScore("HC8", HC8(metadata.Request, dynamicTasksTimeline));
        var hc9 = new ResponseConstraintScore("HC9", HC9(dynamicTasksTimeline));

        var sc1 = new ResponseConstraintScore("SC1", SC1(dynamicTasksTimeline));
        var sc2 = new ResponseConstraintScore("SC2", SC2(metadata.Request, tasksTimeline));
        var sc3 = new ResponseConstraintScore("SC3", SC3(metadata.Request, tasksTimeline));
        var sc4 = new ResponseConstraintScore("SC4", SC4(metadata.Request, tasksTimeline));
        var sc5 = new ResponseConstraintScore("SC5", SC5(metadata.Request, dynamicTasksTimeline));
        var sc6 = new ResponseConstraintScore("SC6", SC6(metadata.Request, dynamicTasksTimeline));
        var sc7 = new ResponseConstraintScore("SC7", SC7(metadata.Request, tasksTimeline));

        return ResponseScore.FromConstraintValues(hc1, hc2, hc3, hc4, hc5, hc6, hc7, hc8, hc9, sc1, sc2, sc3, sc4, sc5, sc6, sc7);
    }

    private int TimeOnlyToMinutes(TimeOnly time)
    {
        return (int)time.ToTimeSpan().TotalMinutes;
    }

    #region Hard Constraints

    //no task time overlaps
    private int HC1(ScheduledTask<TaskBase>[] tasksTimeline)
    {
        var totalScore = 0;
        for(var i = 0; i < tasksTimeline.Length - 1; i++)
        {
            if (tasksTimeline[i].End > tasksTimeline[i + 1].Start)
                totalScore++;
        }

        return totalScore;
    }

    //all required tasks must be scheduled
    private int HC2(GenerateScheduleRequest request, ScheduledTask<DynamicTask>[] tasksTimeline)
    {
        var requiredUnscheduledTasks = request.DynamicTasks.Where(t => t.IsRequired && t.Repeating is null).Select(t => t)
            .Except(tasksTimeline.Select(dt => dt.Task));

        return requiredUnscheduledTasks.Count();
    }

    //tasks must be scheduled within their time windows
    private int HC3(ScheduledTask<DynamicTask>[] tasksTimeline)
    {
        return (int)tasksTimeline.Select(t =>
            Math.Max(0, TimeOnlyToMinutes(t.Task.WindowStart ?? TimeOnly.MinValue) - t.Start.TimeOfDay.TotalMinutes)
                + Math.Max(0, t.End.TimeOfDay.TotalMinutes - TimeOnlyToMinutes(t.Task.WindowEnd ?? TimeOnly.MaxValue)))
            .Sum();
    }

    //task must be done before deadline
    private int HC4(ScheduledTask<DynamicTask>[] tasksTimeline)
    {
        return (int)tasksTimeline.Where(t => t.Task.Deadline is not null)
            .Sum(t => Math.Max(0, (t.End - t.Task.Deadline!.Value).TotalMinutes));
    }

    //task cannot be scheduled outside its category time window
    private int HC5(GenerateScheduleRequest request, ScheduledTask<DynamicTask>[] tasksTimeline)
    {
        var categoryTimeWindowsDict = request.CategoryWindows.GroupBy(cw => cw.Category)
            .ToDictionary(g => g.Key, g => g.ToArray());
        return tasksTimeline.Count(t => t.Task.Categories
            .All(c =>
            {
                var categoryTimeWindows = categoryTimeWindowsDict[c];
                return !categoryTimeWindows.Any(ctw => t.Start >= ctw.StartDateTime && t.End <= ctw.EndDateTime);
            }));
    }

    //week repeating task must be scheduled at least minWeekCount times in the week and at most optWeekCount times
    private int HC6(GenerateScheduleRequest request, ScheduledTask<DynamicTask>[] tasksTimeline)
    {
        var weeks = request.PlanningHorizon.GetWeeks().ToArray();

        var nonAddedTasks = request.DynamicTasks.Where(t => t.Repeating is not null)
            .Except(tasksTimeline.Select(tt => tt.Task));

        return tasksTimeline.Where(t => t.Task.Repeating is not null).GroupBy(t => t.Task)
            .Sum(g =>
            {
                var sum = 0;
                foreach (var week in weeks)
                {
                    var countInWeek = g.Count(t => t.Start.Date >= week.Start.ToDateTime(TimeOnly.MinValue) && t.Start.Date <= week.End.ToDateTime(TimeOnly.MaxValue));
                    sum += Math.Max(0, g.Key.Repeating!.MinWeekCount - countInWeek) + Math.Max(0, countInWeek - g.Key.Repeating!.OptWeekCount);
                }
                return sum;
            }) + nonAddedTasks.Sum(t => t.Repeating!.MinWeekCount * weeks.Length);
    }

    //day repeating task must be scheduled at least minDayCount times in the day and at most optDayCount times
    private int HC7(GenerateScheduleRequest request, ScheduledTask<DynamicTask>[] tasksTimeline)
    {
        var days = request.PlanningHorizon.GetDays().ToArray();

        var nonAddedTasks = request.DynamicTasks.Where(t => t.Repeating is not null)
            .Except(tasksTimeline.Select(tt => tt.Task));

        return tasksTimeline.Where(t => t.Task.Repeating is not null).GroupBy(t => t.Task)
            .Sum(g =>
            {
                var sum = 0;
                foreach (var day in days)
                {
                    var countInDay = g.Count(t => DateOnly.FromDateTime(t.Start.Date) == day);
                    sum += Math.Max(0, g.Key.Repeating!.MinDayCount - countInDay) + Math.Max(0, countInDay - g.Key.Repeating!.OptDayCount);
                }
                return sum;
            }) + nonAddedTasks.Sum(t => t.Repeating!.MinDayCount * days.Length);
    }

    //task cannot be placed outside of planning horizon
    private int HC8(GenerateScheduleRequest request, ScheduledTask<DynamicTask>[] tasksTimeline)
    {
        var startDateTime = request.PlanningHorizon.StartDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = request.PlanningHorizon.EndDate.ToDateTime(TimeOnly.MaxValue);
        return (int)tasksTimeline.Sum(t => 
            Math.Max(0, (startDateTime - t.Start).TotalMinutes) + Math.Max(0, (t.End - endDateTime).TotalMinutes));
    }

    //non repeating tasks can be included max one time
    private int HC9(ScheduledTask<DynamicTask>[] tasksTimeline)
    {
        return tasksTimeline.Where(t => t.Task.Repeating is null).GroupBy(t => t.Task.Id).Count(t => t.Count() > 1);
    }

    #endregion

    #region Soft Constraints

    //maximize total priority of scheduled tasks
    private int SC1(ScheduledTask<DynamicTask>[] tasksTimeline)
    {
        return -1 * tasksTimeline.Sum(t => 6 - t.Task.Priority);
    }

    //minimize total difficulty of scheduled tasks above daily difficulty capacities
    private int SC2(GenerateScheduleRequest request, ScheduledTask<TaskBase>[] tasksTimeline)
    {
        return request.DifficultyCapacities.Sum(dc =>
        {
            var dayStart = dc.Date.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dc.Date.ToDateTime(TimeOnly.MaxValue);
            var dayDifficulty = tasksTimeline.Where(t => t.Start >= dayStart && t.End <= dayEnd).Sum(t => t.Task.Difficulty);
            return Math.Max(0, dayDifficulty - dc.Capacity);
        });
    }

    //follow strategy for scheduling difficult tasks
    private int SC3(GenerateScheduleRequest request, ScheduledTask<TaskBase>[] tasksTimeline)
    {
        var sum = 0;

        var difficultTasksByDays = tasksTimeline.Where(t => t.Task.Difficulty >= 6).GroupBy(t => t.Start.Date).Select(t => t.ToArray());
        foreach(var tasks in difficultTasksByDays)
        {
            for(var i = 0; i < tasks.Length - 1; i++)
            {
                sum += (int)(tasks[i + 1].Start - tasks[i].End).TotalMinutes;
            }
        }

        var coefficient = request.DifficultTaskSchedulingStrategy == DifficultTaskSchedulingStrategy.Cluster ? 1 : -1;
        return coefficient * sum;

        //actually we want for both to either penalize or reward
        //TODO: maybe this could be separate objective
    }

    //maximize user defined task type preferences
    private int SC4(GenerateScheduleRequest request, ScheduledTask<TaskBase>[] tasksTimeline)
    {
        var typePreferenceWeightByDay = request.TaskTypePreferences.ToDictionary(g => g.Date, g => g.Preferences);
        return -1 * tasksTimeline.Sum(t =>
        {
            if(typePreferenceWeightByDay.TryGetValue(DateOnly.FromDateTime(t.Start.Date), out var preferencesForDay))
            {
                var taskTypePreferenceWeights = preferencesForDay.Where(p => t.Task.Types.Contains(p.Type)).Select(p => p.Weight);
                return taskTypePreferenceWeights.Sum();
            }
            return 0;
        });
    }

    //minimize difference between actual and optimal number of scheduled occurrences for week repeating tasks
    private int SC5(GenerateScheduleRequest request, ScheduledTask<DynamicTask>[] tasksTimeline)
    {
        var weeks = request.PlanningHorizon.GetWeeks().ToArray();

        var nonAddedTasks = request.DynamicTasks.Where(t => t.Repeating is not null)
            .Except(tasksTimeline.Select(tt => tt.Task));

        return tasksTimeline.Where(t => t.Task.Repeating is not null).GroupBy(t => t.Task)
            .Sum(g =>
            {
                var sum = 0;
                foreach (var week in weeks)
                {
                    var countInWeek = g.Count(t => t.Start.Date >= week.Start.ToDateTime(TimeOnly.MinValue) && t.Start.Date <= week.End.ToDateTime(TimeOnly.MaxValue));
                    sum += Math.Max(0, g.Key.Repeating!.OptWeekCount - countInWeek);
                }
                return sum;
            }) + nonAddedTasks.Sum(t => t.Repeating!.OptWeekCount * weeks.Length);
    }

    //minimize difference between actual and optimal number of scheduled occurrences for day repeating tasks
    private int SC6(GenerateScheduleRequest request, ScheduledTask<DynamicTask>[] tasksTimeline)
    {
        var days = request.PlanningHorizon.GetDays().ToArray();

        var nonAddedTasks = request.DynamicTasks.Where(t => t.Repeating is not null)
            .Except(tasksTimeline.Select(tt => tt.Task));

        return tasksTimeline.Where(t => t.Task.Repeating is not null).GroupBy(t => t.Task)
            .Sum(g =>
            {
                var sum = 0;
                foreach (var day in days)
                {
                    var countInDay = g.Count(t => DateOnly.FromDateTime(t.Start.Date) == day);
                    sum += Math.Max(0, g.Key.Repeating!.OptDayCount - countInDay);
                }
                return sum;
            }) + nonAddedTasks.Sum(t => t.Repeating!.OptDayCount * days.Length);
    }

    //minimize difficulty difference between days
    private int SC7(GenerateScheduleRequest request, ScheduledTask<TaskBase>[] tasksTimeline)
    {
        var dayTotalDifficulties = request.PlanningHorizon.GetDays()
            .Select(d => tasksTimeline.Where(t => DateOnly.FromDateTime(t.Start.Date) == d).Sum(t => t.Task.Difficulty));
        var averageDifficulty = dayTotalDifficulties.Average();

        return (int)Math.Ceiling(dayTotalDifficulties.Sum(d => Math.Pow((d - averageDifficulty), 2)));
    }

    #endregion
}
