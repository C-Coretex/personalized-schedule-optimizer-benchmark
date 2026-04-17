using Specialized.Optimizer.Models;
using Specialized.Optimizer.Models.Enums;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;

namespace Specialized.Optimizer.Optimizer.Models.Domain;

internal sealed record Domain
{
    //static data
    public Day[] Days { get; init; } = [];
    public Category[] Categories { get; init; } = [];
    public Task[] Tasks { get; init; } = [];

    //settings
    public DifficultTaskSchedulingStrategy DifficultTaskSchedulingStrategy { get; init; }

    public Domain(GenerateScheduleRequest request)
    {
        //list days
        Days = Enumerable.Range(request.PlanningHorizon.StartDate.DayNumber, request.PlanningHorizon.EndDate.DayNumber - request.PlanningHorizon.StartDate.DayNumber + 1)
            .Select(DateOnly.FromDayNumber)
            .Select(date => new Day()
            {
                Date = date,
                DifficultyCapacity = request.DifficultyCapacities.FirstOrDefault(dc => dc.Date == date)?.Capacity ?? int.MaxValue,
                TypeWeights = (request.TaskTypePreferences.FirstOrDefault(tp => tp.Date == date)?.Preferences ?? [])
                    .ToDictionary(p => p.Type, p => p.Weight).ToFrozenDictionary(),
                FixedTasks = request.FixedTasks.Where(t => DateOnly.FromDateTime(t.StartTime) == date).OrderBy(t => t.StartTime)
                    .Select(t => (TimeOnly.FromDateTime(t.StartTime), TimeOnly.FromDateTime(t.EndTime), t))
                    .ToImmutableArray()

            })
            .ToArray();

        //list categories
        Categories = request.CategoryWindows.GroupBy(cw => cw.Category).Select(g => new Category()
        {
            CategoryType = g.Key,
            DayTimeWindows = DayTimeWindow.FromTimeWindows(g.Select(cw => (cw.StartDateTime, cw.EndDateTime)), Days)
                .OrderBy(tw => tw.Day.Date).ThenBy(tw => tw.Start).ToImmutableArray(),
        }).ToArray();
        foreach(var category in Categories)
        {
            foreach (var timeWindow in category.DayTimeWindows)
            {
                timeWindow.Day.AddCategory(category);
            }
        }

        var calendar = CultureInfo.CurrentCulture.Calendar;
        //list free time windows for each day based on fixed tasks and category time windows
        Days = Days.Select(day => day.EnrichWithData(request.FixedTasks, Categories, calendar.GetWeekOfYear(day.Date.ToDateTime(TimeOnly.MinValue), CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)))
            .ToArray();

        //list tasks
        Tasks = request.DynamicTasks.Select(dt => Task.FromDynamicTask(dt, Categories, Days)).Where(t => t.FreeTimeWindows.Length > 0).ToArray();

        DifficultTaskSchedulingStrategy = request.DifficultTaskSchedulingStrategy;
    }
}
