namespace Specialized.Features.Models.Tasks;

public record FixedTask : TaskBase
{
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
}
