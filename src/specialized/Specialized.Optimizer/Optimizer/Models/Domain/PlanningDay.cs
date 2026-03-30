namespace Specialized.Optimizer.Optimizer.Models.Domain
{
    internal record struct PlanningDay
    {
        public PlanningDay(Day day)
        {
            Day = day;
        }

        public Day Day { get; init; }

        private bool _isSorted = false;

        private List<ScheduledTask> _scheduledTasks = new(8);
        //actual planning property
        //can contain unordered values
        //can contain unfeasible values (overlapping tasks)
        public readonly IReadOnlyCollection<ScheduledTask> ScheduledTasks => _scheduledTasks;


        //actual free time windows (possible-scheduled tasks)
        //update on ScheduledTasks change
        //return values ordered by start time
        private FreeTimeWindow[]? _actualTimeWindows;
        public IReadOnlyCollection<FreeTimeWindow> ActualTimeWindows => _actualTimeWindows ??= GetActualTimeWindows().ToArray();

        public PlanningDay GetSnapshot()
        {
            return this with
            {
                _scheduledTasks = [.. _scheduledTasks],
            };
        }

        public void AddScheduledTask(Task task, TimeOnly start)
        {
            _scheduledTasks.Add(new ScheduledTask
            {
                Task = task,
                Start = start,
                End = start.AddMinutes(task.Duration)
            });
            _isSorted = false;
            _actualTimeWindows = null;
        }

        /// <returns>True if added</returns>
        public bool AddScheduledTaskInTimeWindow(Task task, TimeOnly from, TimeOnly? to = null, bool stopIfUnfeasible = false)
        {
            //TODO: cache if will be hot path
            var actualTimeWindow = ScheduledTask.GetActualTimeWindowsForDay(this, task, from, to).FirstOrDefault();
            if(stopIfUnfeasible && actualTimeWindow == default)
                return false;

            var start = actualTimeWindow != default ? actualTimeWindow.Start : from;
            AddScheduledTask(task, start);

            return true;
        }

        public void RemoveScheduledTask(ScheduledTask task)
        {
            _scheduledTasks.Remove(task);
            _isSorted = false;
            _actualTimeWindows = null;
        }

        private IEnumerable<FreeTimeWindow> GetActualTimeWindows()
        {
            if (!_isSorted)
            {
                _scheduledTasks = _scheduledTasks.OrderBy(st => st.Start).ToList();
                _isSorted = true;
            }

            var scheduledTaskEnumerator = ScheduledTasks.GetEnumerator();
            var isScheduledTaskPresent = scheduledTaskEnumerator.MoveNext();
            var currentScheduledTask = isScheduledTaskPresent ? scheduledTaskEnumerator.Current : default;

            //already ordered by start time
            foreach (var possibleTimeWindow in Day.PossibleTimeWindows)
            {
                //no need to check possibleTimeWindow.Start > currentScheduledTask.Start, because everything is ordered
                //we presume that ScheduledTasks always have correct data (in possible time window)
                if (!isScheduledTaskPresent || currentScheduledTask.Start >= possibleTimeWindow.End)
                {
                    yield return possibleTimeWindow;
                    continue;
                }

                //if start < end then task is scheduled inside the current time window (no need to check Start times because the solution is feasible)
                var currentPossibleTimeWindow = possibleTimeWindow;
                while (isScheduledTaskPresent && currentScheduledTask.End <= currentPossibleTimeWindow.End)
                {
                    (var entry1, currentPossibleTimeWindow) = currentPossibleTimeWindow.CutOut(currentScheduledTask.Start, currentScheduledTask.End);
                    yield return entry1;
                    isScheduledTaskPresent = scheduledTaskEnumerator.MoveNext();
                    currentScheduledTask = isScheduledTaskPresent ? scheduledTaskEnumerator.Current : default;
                }
                yield return currentPossibleTimeWindow;
            }
        }
    }

    internal readonly record struct ScheduledTask
    {
        public Task Task { get; init; }
        public TimeOnly Start { get; init; }
        public TimeOnly End { get; init; }

        public static IEnumerable<CategoryTimeWindow> GetActualTimeWindowsForDay(PlanningDay day, Task task, TimeOnly? from = null, TimeOnly? to = null)
        {
            var taskFreeTimeWindows = task.FreeTimeWindows.Where(ftw => ftw.Day.Date == day.Day.Date).AsEnumerable();
            if(from is not null)
                taskFreeTimeWindows = taskFreeTimeWindows.Where(ftw => ftw.End > from)
                    .Select(ftw => from > ftw.Start ? ftw with { Start = from.Value } : ftw);
            if(to is not null)
                taskFreeTimeWindows = taskFreeTimeWindows.Where(ftw => ftw.Start < to)
                    .Select(ftw => to < ftw.End ? ftw with { End = to.Value } : ftw);

            var taskFreeTimeWindowEnumerator = taskFreeTimeWindows.GetEnumerator();
            var isTaskFreeTimeWindowPresent = taskFreeTimeWindowEnumerator.MoveNext();
            var currentTaskFreeTimeWindow = isTaskFreeTimeWindowPresent ? taskFreeTimeWindowEnumerator.Current : default;

            foreach (var actualTimeWindow in day.ActualTimeWindows)
            {
                if (!isTaskFreeTimeWindowPresent)
                    break;
                if (currentTaskFreeTimeWindow.Start >= actualTimeWindow.End)
                    continue;

                while (isTaskFreeTimeWindowPresent && currentTaskFreeTimeWindow.End <= actualTimeWindow.End)
                {
                    var timeWindow = currentTaskFreeTimeWindow with
                    {
                        Start = currentTaskFreeTimeWindow.Start < actualTimeWindow.Start ? actualTimeWindow.Start : currentTaskFreeTimeWindow.Start,
                        End = currentTaskFreeTimeWindow.End > actualTimeWindow.End ? actualTimeWindow.End : currentTaskFreeTimeWindow.End
                    };

                    if(timeWindow.End - timeWindow.Start >= TimeSpan.FromMinutes(task.Duration))
                        yield return timeWindow;

                    isTaskFreeTimeWindowPresent = taskFreeTimeWindowEnumerator.MoveNext();
                    currentTaskFreeTimeWindow = isTaskFreeTimeWindowPresent ? taskFreeTimeWindowEnumerator.Current : default;
                }
            }
        }
    }
}
