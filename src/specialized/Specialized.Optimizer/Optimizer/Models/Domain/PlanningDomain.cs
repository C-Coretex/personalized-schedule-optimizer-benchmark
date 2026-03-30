namespace Specialized.Optimizer.Optimizer.Models.Domain
{
    //separate from Domain to optimize snapshots
    internal readonly record struct PlanningDomain
    {
        public PlanningDomain(Domain domain)
        {
            Domain = domain;
            PlanningDays = [.. domain.Days.Select(day => new PlanningDay(day))];
        }

        public Domain Domain { get; init; }

        //actual planning entity
        public PlanningDay[] PlanningDays { get; init; } = [];

        public PlanningDomain GetSnapshot()
        {
            return this with
            {
                PlanningDays = [.. PlanningDays.Select(pd => pd.GetSnapshot())]
            };
        }

        //calculate total score method here
    }
}
