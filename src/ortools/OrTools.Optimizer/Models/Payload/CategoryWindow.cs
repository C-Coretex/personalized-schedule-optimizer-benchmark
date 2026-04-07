using OrTools.Optimizer.Models.Enums;

namespace OrTools.Optimizer.Models.Payload;

public record CategoryWindow(Category Category, DateTime StartDateTime, DateTime EndDateTime);
