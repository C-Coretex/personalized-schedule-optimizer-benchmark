using OrTools.Console.Models.Enums;

namespace OrTools.Console.Models.Payload;

public record TaskTypeWeight(TaskType Type, int Weight)
{
    public Optimizer.Models.Payload.TaskTypeWeight ToProviderModel() =>
        new(Type.ToProviderModel(), Weight);
}
