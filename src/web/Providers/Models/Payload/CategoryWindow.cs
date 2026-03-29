using Web.Providers.Models.Enums;

namespace Web.Providers.Schedule.Models.Payload;

public record CategoryWindow(Category Category, DateTime StartDateTime, DateTime EndDateTime);
