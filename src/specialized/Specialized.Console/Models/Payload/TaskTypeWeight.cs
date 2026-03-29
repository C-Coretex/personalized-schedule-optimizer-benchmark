using Specialized.Console.Models.Enums;

namespace Specialized.Console.Models.Payload;

public record TaskTypeWeight(TaskType Type, int Weight)
{
    public Specialized.Optimizer.Models.Payload.TaskTypeWeight ToProviderModel() =>
        new(Type.ToProviderModel(), Weight);
}
