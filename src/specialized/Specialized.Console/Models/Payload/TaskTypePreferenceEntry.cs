namespace Specialized.Console.Models.Payload;

public record TaskTypePreferenceEntry(DateOnly Date, IReadOnlyList<TaskTypeWeight> Preferences)
{
    public Specialized.Optimizer.Models.Payload.TaskTypePreferenceEntry ToProviderModel() =>
        new(Date, Preferences.Select(p => p.ToProviderModel()).ToList());
}
