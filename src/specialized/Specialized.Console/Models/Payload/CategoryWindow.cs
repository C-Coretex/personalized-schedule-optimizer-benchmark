using Specialized.Console.Models.Enums;

namespace Specialized.Console.Models.Payload;

public record CategoryWindow(Category Category, DateTime StartDateTime, DateTime EndDateTime)
{
    public Specialized.Optimizer.Models.Payload.CategoryWindow ToProviderModel() =>
        new(Category.ToProviderModel(), StartDateTime, EndDateTime);
}
