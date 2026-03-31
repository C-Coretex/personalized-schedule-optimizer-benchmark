using System.Collections;
using System.Globalization;

namespace Web.Providers.Schedule.Models.Payload;

public record PlanningHorizon(DateOnly StartDate, DateOnly EndDate)
{
    public IEnumerable<DateOnly> GetDays()
    {
        var currentDay = StartDate;
        while (currentDay <= EndDate)
        {
            currentDay = currentDay.AddDays(1);
            yield return currentDay;
        }
    }

    public IEnumerable<(DateOnly Start, DateOnly End, int WeekNumber)> GetWeeks()
    {
        var calendar = CultureInfo.CurrentCulture.Calendar;
        var weekStartDay = StartDate;
        var weekNumber = -1;
        foreach(var day in GetDays())
        {
            var newWeekNumber = calendar.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue), CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            if (newWeekNumber != weekNumber)
            {
                yield return (weekStartDay, day.AddDays(-1), weekNumber);
                weekStartDay = day;
                weekNumber = newWeekNumber;
            }
        }
    }
}
