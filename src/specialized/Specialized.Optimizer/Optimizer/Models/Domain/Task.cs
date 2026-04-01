using Specialized.Optimizer.Models.Enums;
using Specialized.Optimizer.Models.Tasks;
using System.Collections.Immutable;

namespace Specialized.Optimizer.Optimizer.Models.Domain
{
    internal record Task
    {
        public required Guid Id { get; init; }

        /// <summary>Priority from 1 (lowest) to 5 (highest).</summary>
        public required int Priority { get; init; }

        /// <summary>Difficulty from 1 (trivial) to 10 (hardest).</summary>
        public required int Difficulty { get; init; }

        public required ImmutableArray<TaskType> Types { get; init; }

        public required bool IsRequired { get; init; }

        /// <summary>Duration in minutes.</summary>
        public required int Duration { get; init; }

        public required TimeOnly? WindowStart { get; init; }
        public required TimeOnly? WindowEnd { get; init; }

        /// <summary>Hard deadline. Null means no deadline.</summary>
        public DateTime? Deadline { get; init; }

        /// <summary>Null means the task does not repeat.</summary>
        public RepeatingSchedule? Repeating { get; init; }

        public ImmutableArray<Category> Categories { get; init; } = [];

        public ImmutableArray<CategoryTimeWindow> FreeTimeWindows { get; init; } = [];

        public static Task FromDynamicTask(DynamicTask dynamicTask, IEnumerable<Category> categories)
        {
            var taskCategories = categories.Where(c => dynamicTask.Categories.Contains(c.CategoryType)).ToArray();

            var categoryTimeWindows = taskCategories.SelectMany(c => 
                c.DayTimeWindows.Select(dtw => dtw.Day).Distinct().SelectMany(d => 
                    d.PossibleTimeWindows.SelectMany(ptw => ptw.CategoryTimeWindows)
                    .Where(ctw => ctw.Category.CategoryType == c.CategoryType))
            );
            // Get the free time windows for the task by intersecting the category free time windows with the task's time window and deadline.
            var freeTaskTimeWindows = categoryTimeWindows
                .Where(ctw => ctw.Start <= (dynamicTask.WindowEnd ?? TimeOnly.MaxValue))
                .Where(ctw => ctw.End > (dynamicTask.WindowStart ?? TimeOnly.MinValue))
                .Where(ctw => dynamicTask.Deadline == null
                    || (ctw.Day.Date < DateOnly.FromDateTime(dynamicTask.Deadline.Value.Date) 
                        || ctw.Day.Date == DateOnly.FromDateTime(dynamicTask.Deadline.Value.Date) && ctw.Start.ToTimeSpan() < dynamicTask.Deadline.Value.TimeOfDay))
                .Select(ctw =>
                {
                    var minTime = (dynamicTask.WindowStart ?? TimeOnly.MinValue);
                    minTime = minTime > ctw.Start ? minTime : ctw.Start;

                    var maxTime = (dynamicTask.WindowEnd ?? TimeOnly.MaxValue);
                    maxTime = maxTime < ctw.End ? maxTime : ctw.End;
                    if (dynamicTask.Deadline != null && ctw.Day.Date == DateOnly.FromDateTime(dynamicTask.Deadline.Value.Date))
                    {
                        var dynamicTaskDeadlineTime = TimeOnly.FromTimeSpan(dynamicTask.Deadline.Value.TimeOfDay);
                        maxTime = dynamicTaskDeadlineTime < maxTime ? dynamicTaskDeadlineTime : maxTime;
                    }

                    var entry = ctw with
                    {
                        Start = minTime,
                        End = maxTime,
                    };

                    return entry;
                })
                .Where(ctw => ctw.End - ctw.Start >= TimeSpan.FromMinutes(dynamicTask.Duration));

            return new Task
            {
                Id = dynamicTask.Id,
                Priority = dynamicTask.Priority,
                Difficulty = dynamicTask.Difficulty,
                Types = dynamicTask.Types.ToImmutableArray(),
                IsRequired = dynamicTask.IsRequired,
                Duration = dynamicTask.Duration,
                WindowStart = dynamicTask.WindowStart,
                WindowEnd = dynamicTask.WindowEnd,
                Deadline = dynamicTask.Deadline,
                Repeating = dynamicTask.Repeating,
                Categories = taskCategories.ToImmutableArray(),
                FreeTimeWindows = freeTaskTimeWindows.OrderBy(ftw => ftw.Day.Date).ThenBy(ftw => ftw.Start).ToImmutableArray()
            };
        }
    }
}
