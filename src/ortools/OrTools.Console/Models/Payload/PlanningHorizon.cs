namespace OrTools.Console.Models.Payload;

public record PlanningHorizon(DateOnly StartDate, DateOnly EndDate)
{
    public Optimizer.Models.Payload.PlanningHorizon ToProviderModel() =>
        new(StartDate, EndDate);
}
