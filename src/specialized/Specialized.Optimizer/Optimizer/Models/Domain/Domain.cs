using Specialized.Optimizer.Models;

namespace Specialized.Optimizer.Optimizer.Models.Domain
{
    internal record Domain
    {
        public Day[] Days { get; private set; } = [];
        public Category[] Categories { get; private set; } = [];

        //all tasks
        public Task[] Tasks { get; private set; } = [];
        
        //tasks in pool (not scheduled yet)


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
                TimeWindows = DayTimeWindow.FromTimeWindows(g.Select(cw => (cw.StartDateTime, cw.EndDateTime)), Days).OrderBy(tw => tw.Start).ToArray(),
            }).ToArray();
            foreach(var category in Categories)
            {
                foreach (var timeWindow in category.TimeWindows)
                {
                    timeWindow.Day.Categories.Add(category);
                }
            }

            //list free time windows for each day based on fixed tasks and category time windows
            foreach (var day in Days)
                day.PossibleTimeWindows = FreeTimeWindow.FromRequest(day.Date, request.FixedTasks, Categories).ToArray();

            //list tasks
            Tasks = request.DynamicTasks.Select(dt => Task.FromDynamicTask(dt, Categories)).ToArray();
        }
    }
}
