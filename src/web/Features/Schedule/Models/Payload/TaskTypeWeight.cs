using Web.Features.Schedule.Models.Enums;

namespace Web.Features.Schedule.Models.Payload;

public record TaskTypeWeight(TaskType Type, int Weight)
{
    public Providers.Schedule.Models.Payload.TaskTypeWeight ToProviderModel() =>
        new(Type.ToProviderModel(), Weight);
}
