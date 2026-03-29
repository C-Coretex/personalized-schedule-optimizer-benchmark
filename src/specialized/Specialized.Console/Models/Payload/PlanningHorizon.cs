namespace Specialized.Console.Models.Payload;

public record PlanningHorizon(DateOnly StartDate, DateOnly EndDate)
{
    public Specialized.Optimizer.Models.Payload.PlanningHorizon ToProviderModel() =>
        new(StartDate, EndDate);
}
