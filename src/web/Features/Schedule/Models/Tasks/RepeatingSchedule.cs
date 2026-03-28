namespace Web.Features.Schedule.Models.Tasks;

public record RepeatingSchedule
{
    public int MinDayCount { get; init; } = 0;
    public int OptDayCount { get; init; } = 0;
    public int MinWeekCount { get; init; } = 0;
    public int OptWeekCount { get; init; } = 0;
}
