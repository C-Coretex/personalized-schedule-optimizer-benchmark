namespace OrTools.Optimizer.Models.Payload;

public record TaskTypePreferenceEntry(DateOnly Date, IReadOnlyList<TaskTypeWeight> Preferences);
