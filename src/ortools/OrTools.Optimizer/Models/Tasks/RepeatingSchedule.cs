namespace OrTools.Optimizer.Models.Tasks;

public record RepeatingSchedule
{
    public int? MinDayCount { get; init; }
    public int OptDayCount { get; init; } = 1;
    public int? MinWeekCount { get; init; }
    public int OptWeekCount { get; init; } = 1;
}
