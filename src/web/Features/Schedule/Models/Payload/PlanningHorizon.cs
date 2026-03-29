namespace Web.Features.Schedule.Models.Payload;

public record PlanningHorizon(DateOnly StartDate, DateOnly EndDate)
{
    public Providers.Schedule.Models.Payload.PlanningHorizon ToProviderModel() =>
        new(StartDate, EndDate);
}
