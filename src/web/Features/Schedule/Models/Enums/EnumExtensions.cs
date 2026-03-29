namespace Web.Features.Schedule.Models.Enums;

public static class EnumExtensions
{
    public static Providers.Models.Enums.Category ToProviderModel(this Category value) =>
        (Providers.Models.Enums.Category)(int)value;

    public static Providers.Models.Enums.TaskType ToProviderModel(this TaskType value) =>
        (Providers.Models.Enums.TaskType)(int)value;

    public static Providers.Models.Enums.DifficultTaskSchedulingStrategy ToProviderModel(this DifficultTaskSchedulingStrategy value) =>
        (Providers.Models.Enums.DifficultTaskSchedulingStrategy)(int)value;
}
