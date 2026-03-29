namespace Web.Providers.Schedule.Models.Payload;

public record TaskTypePreferenceEntry(DateOnly Date, IReadOnlyList<TaskTypeWeight> Preferences);
