using Specialized.Optimizer.Models.Enums;
using Specialized.Optimizer.Models.Tasks;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Specialized.Optimizer.Optimizer.Models.Domain;

internal record Day
{
    public DateOnly Date { get; init; }
    public int DifficultyCapacity { get; init; }

    public int WeekNumber { get; private set; }
    public ImmutableHashSet<Category> Categories { get; private set; } = [];
    public ImmutableArray<FreeTimeWindow> PossibleTimeWindows { get; private set; } = [];

    public FrozenDictionary<TaskType, int> TypeWeights { get; init; } = new Dictionary<TaskType, int>().ToFrozenDictionary();

    public Day AddCategory(Category category)
    {
        Categories = Categories.Add(category);
        return this;
    }

    public Day EnrichWithData(IEnumerable<FixedTask> tasks, ICollection<Category> categories, int weekNumber)
    {
        PossibleTimeWindows = FreeTimeWindow.FromRequest(this, tasks, categories).OrderBy(ftw => ftw.Start).ToImmutableArray();
        WeekNumber = weekNumber;
        return this;
    }
}

internal readonly record struct FreeTimeWindow
{
    public TimeOnly Start { get; init; }
    public TimeOnly End { get; init; }
    public ImmutableArray<CategoryTimeWindow> CategoryTimeWindows { get; init; }
    public Day Day { get; init; }

    public (FreeTimeWindow First, FreeTimeWindow Second) CutOut(TimeOnly from, TimeOnly to)
    {
        return (
            this with 
            {
                End = from,
                CategoryTimeWindows = CategoryTimeWindows.Where(ctw => ctw.Start < from)
                    .Select(ctw => ctw with { End = from < ctw.End ? from : ctw.End }).ToImmutableArray()
            },
            this with
            {
                Start = to,
                CategoryTimeWindows = CategoryTimeWindows.Where(ctw => ctw.Start >= to)
                    .Select(ctw => ctw with { Start = to > ctw.Start ? to : ctw.Start }).ToImmutableArray()
            }
        );
    }

    public (FreeTimeWindow First, FreeTimeWindow Second) Split(TimeOnly splitTime)
    {
        return (
            this with
            {
                End = splitTime,
                CategoryTimeWindows = CategoryTimeWindows.Where(ctw => ctw.Start < splitTime)
                    .Select(ctw => ctw with { End = splitTime < ctw.End ? splitTime : ctw.End }).ToImmutableArray()
            },
            this with
            {
                Start = splitTime,
                CategoryTimeWindows = CategoryTimeWindows.Where(ctw => ctw.Start >= splitTime)
                    .Select(ctw => ctw with { Start = splitTime > ctw.Start ? splitTime : ctw.Start }).ToImmutableArray()
            }
        );
    }

    public static IEnumerable<FreeTimeWindow> FromRequest(Day day, IEnumerable<FixedTask> tasks, ICollection<Category> categories)
    {
        var startDate = day.Date.ToDateTime(TimeOnly.MinValue);
        tasks = tasks.Where(ft => ft.StartTime.Date == startDate).OrderBy(ft => ft.StartTime);
        categories = categories.Select(c => c with { DayTimeWindows = c.DayTimeWindows.Where(tw => tw.Day.Date == day.Date).ToImmutableArray() }).Where(c => c.DayTimeWindows.Length > 0).ToImmutableArray();

        var startTime = TimeOnly.MinValue;
        foreach(var task in tasks)
        {
            var endTime = TimeOnly.FromDateTime(task.StartTime);
            if (endTime > startTime)
            {
                var categoryTimeWindows = CategoryTimeWindow.FromRequest(day, startTime, endTime, categories).OrderBy(ctw => ctw.Start).ToImmutableArray();
                if(categoryTimeWindows.Length > 0)
                    yield return new FreeTimeWindow
                    {
                        Start = categoryTimeWindows.Min(ctw => ctw.Start),
                        End = categoryTimeWindows.Max(ctw => ctw.End),
                        CategoryTimeWindows = categoryTimeWindows,
                        Day = day
                    };
            }
            startTime = TimeOnly.FromDateTime(task.EndTime);
        }

        if(startTime < TimeOnly.MaxValue)
        {
            var categoryTimeWindows = CategoryTimeWindow.FromRequest(day, startTime, TimeOnly.MaxValue, categories).OrderBy(ctw => ctw.Start).ToImmutableArray();
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
    public Day Day { get; init; }

    public static IEnumerable<CategoryTimeWindow> FromRequest(Day day, TimeOnly start, TimeOnly end, IEnumerable<Category> categories)
    {
        foreach(var category in categories)
        {
            var categoryTimeWindows = category.DayTimeWindows.Where(tw => tw.Start < end && tw.End > start);
            foreach (var categoryTimeWindow in categoryTimeWindows)
                yield return new()
                {
                    Day = day,
                    Category = category,
                    Start = start > categoryTimeWindow.Start ? start : categoryTimeWindow.Start,
                    End = end < categoryTimeWindow.End ? end : categoryTimeWindow.End,
                };
        }
    }
}