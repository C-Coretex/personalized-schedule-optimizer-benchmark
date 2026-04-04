namespace Specialized.Optimizer.Optimizer.Models.Domain;

//separate from Domain to optimize snapshots
internal partial record PlanningDomain
{
    public PlanningDomain(Domain domain)
    {
        Domain = domain;

        var weekTasks = domain.Tasks.Where(t => t.IsWeekRepeating).ToArray();
        WeekRepeatingTasksCount = domain.Days.GroupBy(d => d.WeekNumber).ToDictionary(g => g.Key, g =>
            weekTasks.ToDictionary(wt => wt.Id, _ => 0));

        PlanningDays = [.. domain.Days.Select(day => new PlanningDay(day, this))];

        InitConstraintValues(domain);
    }

    public void OnTaskAdded(ScheduledTask task, PlanningDay day)
        => UpdateConstraintValues(task, day, add: true);

    public void OnTaskRemoved(ScheduledTask task, PlanningDay day)
        => UpdateConstraintValues(task, day, add: false);

    public Domain Domain { get; init; }

    //actual planning entity
    public PlanningDay[] PlanningDays { get; private set; } = [];

    public Dictionary<Task, int> AvailableTasksPool { get; private set; } = [];

    public PlanningDomain GetSnapshot()
    {
        var snapshot = this with
        {
            AvailableTasksPool = new(AvailableTasksPool),
            WeekRepeatingTasksCount = new(WeekRepeatingTasksCount)
        };
        snapshot.PlanningDays = [.. PlanningDays.Select(pd => pd.GetSnapshot(snapshot))];

        return snapshot;
    }
}
