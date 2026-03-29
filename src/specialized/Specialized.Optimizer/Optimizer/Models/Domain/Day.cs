using Specialized.Optimizer.Models.Tasks;

namespace Specialized.Optimizer.Optimizer.Models.Domain;

internal record Day
{
    public DateOnly Date { get; set; }
    public HashSet<Category> Categories { get; set; } = [];
    public FreeTimeWindow[] PossibleTimeWindows { get; set; } = [];

    //scheduled dynamic tasks (ordered by start time)

    //actual free time windows (possible-scheduled tasks)
}

internal readonly record struct FreeTimeWindow
{
    public TimeOnly Start { get; init; }
    public TimeOnly End { get; init; }
    public CategoryTimeWindow[] CategoryTimeWindows { get; init; }

    public static IEnumerable<FreeTimeWindow> FromRequest(DateOnly date, IEnumerable<FixedTask> tasks, ICollection<Category> categories)
    {
        var startDate = date.ToDateTime(TimeOnly.MinValue);
        tasks = tasks.Where(ft => ft.StartTime.Date == startDate).OrderBy(ft => ft.StartTime);
        categories = categories.Select(c => c with { TimeWindows = c.TimeWindows.Where(tw => tw.Day.Date == date).ToArray() }).Where(c => c.TimeWindows.Length > 0).ToArray();

        var startTime = TimeOnly.MinValue;
        foreach(var task in tasks)
        {
            var endTime = TimeOnly.FromDateTime(task.StartTime);
            if (endTime > startTime)
            {
                var categoryTimeWindows = CategoryTimeWindow.FromRequest(startTime, endTime, categories).ToArray();
                if(categoryTimeWindows.Length > 0)
                    yield return new FreeTimeWindow
                    {
                        Start = categoryTimeWindows.Min(ctw => ctw.Start),
                        End = categoryTimeWindows.Max(ctw => ctw.End),
                        CategoryTimeWindows = categoryTimeWindows
                    };
            }
            startTime = TimeOnly.FromDateTime(task.EndTime);
        }

        if(startTime < TimeOnly.MaxValue)
        {
            var categoryTimeWindows = CategoryTimeWindow.FromRequest(startTime, TimeOnly.MaxValue, categories).ToArray();
            if (categoryTimeWindows.Length > 0)
                yield return new FreeTimeWindow
                {
                    Start = categoryTimeWindows.Min(ctw => ctw.Start),
                    End = categoryTimeWindows.Max(ctw => ctw.End),
                    CategoryTimeWindows = categoryTimeWindows
                };
        }
    }
}

internal readonly record struct CategoryTimeWindow
{
    public Category Category { get; init; }
    public TimeOnly Start { get; init; }
    public TimeOnly End { get; init; }

    public static IEnumerable<CategoryTimeWindow> FromRequest(TimeOnly start, TimeOnly end, IEnumerable<Category> categories)
    {
        foreach(var category in categories)
        {
            var categoryTimeWindows = category.TimeWindows.Where(tw => tw.Start < end && tw.End > start);
            foreach(var categoryTimeWindow in categoryTimeWindows)
                yield return new CategoryTimeWindow
                {
                    Category = category,
                    Start = start > categoryTimeWindow.Start ? start : categoryTimeWindow.Start,
                    End = end < categoryTimeWindow.End ? end : categoryTimeWindow.End,
                };
        }
    }
}
