using Web.Features.Schedule.Models.Enums;

namespace Web.Features.Schedule.Models.Payload;

public record CategoryWindow(Category Category, DateTime StartDateTime, DateTime EndDateTime)
{
    public Providers.Schedule.Models.Payload.CategoryWindow ToProviderModel() =>
        new(Category.ToProviderModel(), StartDateTime, EndDateTime);
}
