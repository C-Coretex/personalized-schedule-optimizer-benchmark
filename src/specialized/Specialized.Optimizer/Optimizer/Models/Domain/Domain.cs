using Specialized.Optimizer.Models;

namespace Specialized.Optimizer.Optimizer.Models.Domain
{
    internal record Domain
    {
        //static data
        public Day[] Days { get; private set; } = [];
        public Category[] Categories { get; private set; } = [];
        public Task[] Tasks { get; private set; } = [];
        
        //actual planning entity
        public PlanningDay[] PlanningDays { get; private set; }


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
                    .OrderBy(tw => tw.Day.Date).ThenBy(tw => tw.Start).ToArray(),
            }).ToArray();
            foreach(var category in Categories)
            {
                foreach (var timeWindow in category.DayTimeWindows)
                {
                    timeWindow.Day.Categories.Add(category);
                }
            }

            //list free time windows for each day based on fixed tasks and category time windows
            Days = Days.Select(day => 
                day with 
                { 
                    PossibleTimeWindows = FreeTimeWindow.FromRequest(day, request.FixedTasks, Categories).OrderBy(ftw => ftw.Start).ToArray() 
                }).ToArray();

            //list tasks
            Tasks = request.DynamicTasks.Select(dt => Task.FromDynamicTask(dt, Categories)).ToArray();

            //init actual planning entity
            PlanningDays = Days.Select(day => new PlanningDay(day)).ToArray();
        }
    }
}
