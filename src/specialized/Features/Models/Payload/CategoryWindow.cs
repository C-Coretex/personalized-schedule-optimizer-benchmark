using Specialized.Features.Models.Enums;

namespace Specialized.Features.Models.Payload;

public record CategoryWindow(Category Category, DateTime StartDateTime, DateTime EndDateTime);
