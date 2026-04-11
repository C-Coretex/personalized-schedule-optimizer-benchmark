using System.Globalization;

namespace OrTools.Optimizer.Models.Payload;

public record PlanningHorizon(DateOnly StartDate, DateOnly EndDate)
{
    public IEnumerable<DateOnly> GetDays()
    {
        var currentDay = StartDate;
        while (currentDay <= EndDate)
        {
            yield return currentDay;
            currentDay = currentDay.AddDays(1);
        }
    }

    public IEnumerable<(DateOnly Start, DateOnly End, int WeekNumber)> GetWeeks()
    {
        var calendar = CultureInfo.CurrentCulture.Calendar;
        var weekStartDay = StartDate;
        int? weekNumber = null;
        foreach (var day in GetDays())
        {
            weekNumber ??= calendar.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue), CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            var newWeekNumber = calendar.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue), CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            if (newWeekNumber != weekNumber)
            {
                yield return (weekStartDay, day.AddDays(-1), weekNumber.Value);
                weekStartDay = day;
                weekNumber = newWeekNumber;
            }
        }
        if (weekNumber.HasValue)
            yield return (weekStartDay, EndDate, weekNumber.Value);
    }
}

