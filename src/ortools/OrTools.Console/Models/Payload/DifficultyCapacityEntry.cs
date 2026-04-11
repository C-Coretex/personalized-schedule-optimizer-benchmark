namespace OrTools.Console.Models.Payload;

/// <summary>Maximum difficulty budget available on a given date.</summary>
public record DifficultyCapacityEntry(DateOnly Date, int Capacity)
{
    public Optimizer.Models.Payload.DifficultyCapacityEntry ToProviderModel() =>
        new(Date, Capacity);
}
