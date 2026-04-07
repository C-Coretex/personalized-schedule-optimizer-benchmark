namespace OrTools.Optimizer.Models.Payload;

/// <summary>Maximum difficulty budget available on a given date.</summary>
public record DifficultyCapacityEntry(DateOnly Date, int Capacity);
