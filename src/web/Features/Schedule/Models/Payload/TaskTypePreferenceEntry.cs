namespace Web.Features.Schedule.Models.Payload;

public record TaskTypePreferenceEntry(DateOnly Date, IReadOnlyList<TaskTypeWeight> Preferences)
{
    public Providers.Schedule.Models.Payload.TaskTypePreferenceEntry ToProviderModel() =>
        new(Date, Preferences.Select(p => p.ToProviderModel()).ToList());
}
