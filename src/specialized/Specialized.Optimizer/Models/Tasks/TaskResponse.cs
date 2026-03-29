namespace Specialized.Optimizer.Models.Tasks;

public record TaskResponse
{
    public required Guid Id { get; init; }
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
}
