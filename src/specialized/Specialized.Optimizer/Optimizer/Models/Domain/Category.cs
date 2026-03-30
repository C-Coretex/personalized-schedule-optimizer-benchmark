using System.Collections.Immutable;

namespace Specialized.Optimizer.Optimizer.Models.Domain;

internal record Category
{
    public required Specialized.Optimizer.Models.Enums.Category CategoryType { get; init; }
    //time windows for day
    public ImmutableArray<DayTimeWindow> DayTimeWindows { get; init; } = [];
}

internal readonly record struct DayTimeWindow
{
    public required Day Day { get; init; }
    public TimeOnly Start { get; init; }
    public TimeOnly End { get; init; }

    public static IEnumerable<DayTimeWindow> FromTimeWindows(IEnumerable<(DateTime Start, DateTime End)> timeWindows, Day[] days)
    {
        foreach(var timeWindow in timeWindows)
        {
            var day = days.FirstOrDefault(d => d.Date == DateOnly.FromDateTime(timeWindow.Start));
            if (day is not null)
            {
                yield return new DayTimeWindow
                {
                    Day = day,
                    Start = TimeOnly.FromDateTime(timeWindow.Start),
                    End = TimeOnly.FromDateTime(timeWindow.End)
                };
            }
        }
    }
}
