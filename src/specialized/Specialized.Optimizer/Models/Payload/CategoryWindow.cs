using Specialized.Optimizer.Models.Enums;

namespace Specialized.Optimizer.Models.Payload;

public record CategoryWindow(Category Category, DateTime StartDateTime, DateTime EndDateTime);
