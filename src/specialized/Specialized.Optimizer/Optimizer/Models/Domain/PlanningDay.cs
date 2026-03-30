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
        //public SortedList<DateTime, ScheduledTask> ScheduledTasks { get; init; } = new(8);
        private List<ScheduledTask> _scheduledTasks = new(8);
        //actual planning property
        public IReadOnlyList<ScheduledTask> ScheduledTasks => _scheduledTasks;

        public void AddScheduledTask(Task task, TimeOnly start, TimeOnly end)
        {
            _scheduledTasks.Add(new ScheduledTask
            {
                Task = task,
                Start = start,
                End = end
            });
            _isSorted = false;
        }

        public void RemoveScheduledTask(ScheduledTask task)
        {
            _scheduledTasks.Remove(task);
            _isSorted = false;
        }

        //actual free time windows (possible-scheduled tasks)
        //update on ScheduledTasks change
        //return values ordered by start time
        public IEnumerable<FreeTimeWindow> ActualTimeWindows
        {
            get
            {
                if(!_isSorted)
                {
                    _scheduledTasks = _scheduledTasks.OrderBy(st => st.Start).ToList();
                    _isSorted = true;
                }

                var scheduledTaskEnumerator = ScheduledTasks.GetEnumerator();
                var isScheduledTaskPresent = scheduledTaskEnumerator.MoveNext();
                var currentScheduledTask = scheduledTaskEnumerator.Current;

                //already ordered by start time
                foreach (var possibleTimeWindow in Day.PossibleTimeWindows)
                {
                    //no need to check possibleTimeWindow.Start > currentScheduledTask.Start, because everything is ordered
                    //we presume that ScheduledTasks always have correct data (in possible time window)
                    if(!isScheduledTaskPresent || currentScheduledTask.Start >= possibleTimeWindow.End)
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
                        currentScheduledTask = scheduledTaskEnumerator.Current;
                    }
                    yield return currentPossibleTimeWindow;
                }
            }
        }
    }

    internal readonly record struct ScheduledTask
    {
        public Task Task { get; init; }
        public TimeOnly Start { get; init; }
        public TimeOnly End { get; init; }

        public IEnumerable<CategoryTimeWindow> GetActualTimeWindowsForDay(PlanningDay day)
        {
            var taskFreeTimeWindows = Task.FreeTimeWindows.Where(ftw => ftw.Day.Date == day.Day.Date);
            var taskFreeTimeWindowEnumerator = taskFreeTimeWindows.GetEnumerator();
            var isTaskFreeTimeWindowPresent = taskFreeTimeWindowEnumerator.MoveNext();
            var currentTaskFreeTimeWindow = taskFreeTimeWindowEnumerator.Current;

            foreach (var actualTimeWindow in day.ActualTimeWindows)
            {
                if (!isTaskFreeTimeWindowPresent)
                    break;
                if (currentTaskFreeTimeWindow.Start >= actualTimeWindow.End)
                    continue;

                while (isTaskFreeTimeWindowPresent && currentTaskFreeTimeWindow.End <= actualTimeWindow.End)
                {
                    yield return currentTaskFreeTimeWindow with
                    {
                        Start = currentTaskFreeTimeWindow.Start < actualTimeWindow.Start ? actualTimeWindow.Start : currentTaskFreeTimeWindow.Start,
                        End = currentTaskFreeTimeWindow.End > actualTimeWindow.End ? actualTimeWindow.End : currentTaskFreeTimeWindow.End
                    };

                    isTaskFreeTimeWindowPresent = taskFreeTimeWindowEnumerator.MoveNext();
                    currentTaskFreeTimeWindow = taskFreeTimeWindowEnumerator.Current;
                }
            }
        }
    }
}
