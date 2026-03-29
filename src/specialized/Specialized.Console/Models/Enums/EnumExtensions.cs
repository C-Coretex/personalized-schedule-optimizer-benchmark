namespace Specialized.Console.Models.Enums;

public static class EnumExtensions
{
    public static Specialized.Optimizer.Models.Enums.Category ToProviderModel(this Category value) =>
        (Specialized.Optimizer.Models.Enums.Category)(int)value;

    public static Specialized.Optimizer.Models.Enums.TaskType ToProviderModel(this TaskType value) =>
        (Specialized.Optimizer.Models.Enums.TaskType)(int)value;

    public static Specialized.Optimizer.Models.Enums.DifficultTaskSchedulingStrategy ToProviderModel(this DifficultTaskSchedulingStrategy value) =>
        (Specialized.Optimizer.Models.Enums.DifficultTaskSchedulingStrategy)(int)value;
}
