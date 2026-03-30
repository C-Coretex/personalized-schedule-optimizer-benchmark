using Specialized.Optimizer.Models;
using System.Collections.Immutable;
using System.Globalization;

namespace Specialized.Optimizer.Optimizer.Models.Domain
{
    internal record Domain
    {
        //static data
        public Day[] Days { get; private set; } = [];
        public Category[] Categories { get; private set; } = [];
        public Task[] Tasks { get; private set; } = [];

        public Domain(GenerateScheduleRequest request)
        {
            //list days
            Days = Enumerable.Range(request.PlanningHorizon.StartDate.DayNumber, request.PlanningHorizon.EndDate.DayNumber - request.PlanningHorizon.StartDate.DayNumber + 1)
                .Select(dayNumber => new Day() { Date = DateOnly.FromDayNumber(dayNumber) })
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

            //var t = request.DynamicTasks.Select(dt => Task.FromDynamicTask(dt, Categories));
            //list tasks
            Tasks = request.DynamicTasks.Select(dt => Task.FromDynamicTask(dt, Categories)).Where(t => t.FreeTimeWindows.Length > 0).ToArray();
        }
    }
}
