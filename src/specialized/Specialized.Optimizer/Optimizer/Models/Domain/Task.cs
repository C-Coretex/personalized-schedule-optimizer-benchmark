using Specialized.Optimizer.Models.Enums;
using Specialized.Optimizer.Models.Tasks;

namespace Specialized.Optimizer.Optimizer.Models.Domain
{
    internal record Task
    {
        public required Guid Id { get; init; }

        /// <summary>Priority from 1 (lowest) to 5 (highest).</summary>
        public required int Priority { get; init; }

        /// <summary>Difficulty from 1 (trivial) to 10 (hardest).</summary>
        public required int Difficulty { get; init; }

        public required TaskType[] Types { get; init; }

        public required bool IsRequired { get; init; }

        /// <summary>Duration in minutes.</summary>
        public required int Duration { get; init; }

        public required TimeOnly? WindowStart { get; init; }
        public required TimeOnly? WindowEnd { get; init; }

        /// <summary>Hard deadline. Null means no deadline.</summary>
        public DateTime? Deadline { get; init; }

        /// <summary>Null means the task does not repeat.</summary>
        public RepeatingSchedule? Repeating { get; init; }

        public Category[] Categories { get; init; } = [];

        public static Task FromDynamicTask(DynamicTask dynamicTask, IEnumerable<Category> categories)
        {
            return new Task
            {
                Id = dynamicTask.Id,
                Priority = dynamicTask.Priority,
                Difficulty = dynamicTask.Difficulty,
                Types = dynamicTask.Types.ToArray(),
                IsRequired = dynamicTask.IsRequired,
                Duration = dynamicTask.Duration,
                WindowStart = dynamicTask.WindowStart,
                WindowEnd = dynamicTask.WindowEnd,
                Deadline = dynamicTask.Deadline,
                Repeating = dynamicTask.Repeating,
                Categories = categories.Where(c => dynamicTask.Categories.Contains(c.CategoryType)).ToArray()
            };
        }
    }
}
