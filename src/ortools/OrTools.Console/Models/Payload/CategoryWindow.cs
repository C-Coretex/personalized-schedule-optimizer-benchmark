using OrTools.Console.Models.Enums;

namespace OrTools.Console.Models.Payload;

public record CategoryWindow(Category Category, DateTime StartDateTime, DateTime EndDateTime)
{
    public Optimizer.Models.Payload.CategoryWindow ToProviderModel() =>
        new(Category.ToProviderModel(), StartDateTime, EndDateTime);
}
