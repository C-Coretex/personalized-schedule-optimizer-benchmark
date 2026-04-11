namespace OrTools.Console.Models.Enums;

public static class EnumExtensions
{
    public static Optimizer.Models.Enums.Category ToProviderModel(this Category value) =>
        (Optimizer.Models.Enums.Category)(int)value;

    public static Optimizer.Models.Enums.TaskType ToProviderModel(this TaskType value) =>
        (Optimizer.Models.Enums.TaskType)(int)value;

    public static Optimizer.Models.Enums.DifficultTaskSchedulingStrategy ToProviderModel(this DifficultTaskSchedulingStrategy value) =>
        (Optimizer.Models.Enums.DifficultTaskSchedulingStrategy)(int)value;
}
