namespace Specialized.Features.Models.Payload;

public record TaskTypePreferenceEntry(DateOnly Date, IReadOnlyList<TaskTypeWeight> Preferences);
