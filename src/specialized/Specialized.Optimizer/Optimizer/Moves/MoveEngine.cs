using Specialized.Optimizer.Helpers;
using Specialized.Optimizer.Optimizer.Models.Domain;

namespace Specialized.Optimizer.Optimizer.Moves;

internal class MoveEngine
{
    public MoveEngine(Random? random = null)
    {
        _random = random ?? new Random();
    }

    private readonly Random _random;

    public PlanningDomain AddTask(PlanningDomain domain, bool replace = true, bool createSnapshot = true)
    {
        if (domain.AvailableTasksPool.Count == 0)
            return domain;

        if(createSnapshot)
            domain = domain.GetSnapshot();

        var task = domain.AvailableTasksPool.RandomElement(_random);
        //could be hot path
        var freeTimeWindows = domain.GetActualFreeTimeWindowsFor(task.Key, _random);
        if (freeTimeWindows.Length == 0)
            return domain;

        var selectedTimeWindow = freeTimeWindows.RandomElement(_random);

        if(!selectedTimeWindow.Day.AddScheduledTask(task.Key, selectedTimeWindow.TimeWindow.Start, stopIfUnfeasible: replace) 
            && replace)
        {
            domain.Replace(task.Key, selectedTimeWindow.Day, selectedTimeWindow.TimeWindow.Start);
        }

        return domain;
    }

    public PlanningDomain RemoveTask(PlanningDomain domain, bool createSnapshot = true)
    {
        if (createSnapshot)
            domain = domain.GetSnapshot();

        var day = domain.PlanningDays.RandomElement(_random);
        if (day.ScheduledTasks.Count == 0)
            return domain;

        var tasks = day.ScheduledTasks.Where(t => !t.Task.IsRequired).ToArray();
        if (tasks.Length == 0)
            return domain;

        var task = tasks.RandomElement(_random);
        day.RemoveScheduledTask(task);

        return domain;
    }

    public PlanningDomain SwapTasks(PlanningDomain domain, MoveScope scope, bool createSnapshot = true)
    {
        if (createSnapshot)
            domain = domain.GetSnapshot();

        var day = domain.PlanningDays.RandomElement(_random);
        if (day.ScheduledTasks.Count == 0)
            return domain;

        var task = day.ScheduledTasks.RandomElement(_random);

        var selectedTimeWindow = scope switch
        {
            MoveScope.Operational => GetTacticalTimeWindow(task, day), //operational and tactical are the same in this move
            MoveScope.Tactical => GetTacticalTimeWindow(task, day),
            MoveScope.SemiStrategic => GetStrategicTimeWindow(task, day), //semi-strategic and strategic are the same in this move
            MoveScope.Strategic => GetStrategicTimeWindow(task, day),

            _ => throw new NotImplementedException(),
        };

        if (selectedTimeWindow.Start is null || selectedTimeWindow.Day is null)
            return domain;

        domain.Swap(task, day, selectedTimeWindow.Day, selectedTimeWindow.Start.Value);

        return domain;

        (TimeOnly? Start, PlanningDay? Day) GetTacticalTimeWindow(ScheduledTask task, PlanningDay day)
        {
            var randomFreeTimeWindow = task.Task.FreeTimeWindowsByDate[day.Day.Date].RandomElement(_random);
          //  if (randomFreeTimeWindow.Start <= task.Start && randomFreeTimeWindow.End >= task.End)
             //   return (null, null);

            var minutesWindow = (int)(randomFreeTimeWindow.End.AddMinutes(-task.Task.Duration) - randomFreeTimeWindow.Start).TotalMinutes;
            return (randomFreeTimeWindow.Start.AddMinutes(_random.Next(minutesWindow)), day);
        }

        (TimeOnly? Start, PlanningDay? Day) GetStrategicTimeWindow(ScheduledTask task, PlanningDay day)
        {
            var randomFreeTimeWindow = task.Task.FreeTimeWindows.RandomElement(_random);
            if (randomFreeTimeWindow.Start <= task.Start && randomFreeTimeWindow.End >= task.End)
                return (null, null);

            var minutesWindow = (int)(randomFreeTimeWindow.End.AddMinutes(-task.Task.Duration) - randomFreeTimeWindow.Start).TotalMinutes;
            return (randomFreeTimeWindow.Start.AddMinutes(_random.Next(minutesWindow)), domain.PlanningDays.First(d => d.Day == randomFreeTimeWindow.Day));
        }
    }

    public PlanningDomain MoveTasks(PlanningDomain domain, int cascadeSequenceMaxCount, MoveScope scope, bool createSnapshot = true)
    {
        if (createSnapshot)
            domain = domain.GetSnapshot();

        var day = domain.PlanningDays.RandomElement(_random);
        if (day.ScheduledTasks.Count == 0)
            return domain;
        var task = day.ScheduledTasks.RandomElement(_random);
        for (var i = 0; i < cascadeSequenceMaxCount; i++)
        {
            var selectedTimeWindow = scope switch
            {
                MoveScope.Operational => GetTacticalTimeWindow(task, day), //operational and tactical are the same in this move
                MoveScope.Tactical => GetTacticalTimeWindow(task, day),
                MoveScope.SemiStrategic => GetStrategicTimeWindow(task, day), //semi-strategic and strategic are the same in this move
                MoveScope.Strategic => GetStrategicTimeWindow(task, day),

                _ => throw new NotImplementedException(),
            };

            if (selectedTimeWindow.Start is null || selectedTimeWindow.Day is null)
                return domain;

            var tasks = domain.Replace(task, day, selectedTimeWindow.Day, selectedTimeWindow.Start.Value);
            if (tasks.Length == 0)
                return domain;

            task = tasks.RandomElement(_random);
            day = selectedTimeWindow.Day;
        }

        var terminalWindows = ScheduledTask.GetActualTimeWindowsForDay(day, task.Task).ToArray();
        if (terminalWindows.Length > 0)
            day.AddScheduledTask(task.Task, terminalWindows.RandomElement(_random).Start, stopIfUnfeasible: true);

        return domain;

        (TimeOnly? Start, PlanningDay? Day) GetTacticalTimeWindow(ScheduledTask task, PlanningDay day)
        {
            var randomFreeTimeWindow = task.Task.FreeTimeWindowsByDate[day.Day.Date].RandomElement(_random);

            var minutesWindow = (int)(randomFreeTimeWindow.End.AddMinutes(-task.Task.Duration) - randomFreeTimeWindow.Start).TotalMinutes;
            return (randomFreeTimeWindow.Start.AddMinutes(_random.Next(minutesWindow)), day);
        }

        (TimeOnly? Start, PlanningDay? Day) GetStrategicTimeWindow(ScheduledTask task, PlanningDay day)
        {
            var randomFreeTimeWindow = task.Task.FreeTimeWindows.RandomElement(_random);

            var minutesWindow = (int)(randomFreeTimeWindow.End.AddMinutes(-task.Task.Duration) - randomFreeTimeWindow.Start).TotalMinutes;
            return (randomFreeTimeWindow.Start.AddMinutes(_random.Next(minutesWindow)), domain.PlanningDays.First(d => d.Day == randomFreeTimeWindow.Day));
        }
    }

    public PlanningDomain RuinRecreate(PlanningDomain domain, MoveScope ruinScope, out int deletedTasksCount, bool createSnapshot = true)
    {
        if (createSnapshot)
            domain = domain.GetSnapshot();

        deletedTasksCount = ruinScope switch
        {
            MoveScope.Operational => RuinOperational(),
            MoveScope.Tactical => RuinTactical(),
            MoveScope.SemiStrategic => RuinSemiStrategic(),
            MoveScope.Strategic => RuinStrategic(),

            _ => throw new NotImplementedException()
        };

        if(deletedTasksCount > 0)
            ConstructionHeuristics.Construct(domain, _random, createSnapshot: false);

        return domain;

        int RuinOperational()
        {
            var day = domain.PlanningDays.RandomElement(_random);
            if (day.Day.Categories.Count == 0)
                return 0;

            var category = day.Day.Categories.RandomElement(_random);
            var tasksToRemove = day.ScheduledTasks.Where(c => c.Task.Categories.Contains(category)).ToArray();
            foreach(var task in tasksToRemove)
                day.RemoveScheduledTask(task);

            return tasksToRemove.Length;
        }

        int RuinTactical()
        {
            var day = domain.PlanningDays.RandomElement(_random);
            var tasksToRemove = day.ScheduledTasks.ToArray();
            foreach (var task in tasksToRemove)
                day.RemoveScheduledTask(task);

            return tasksToRemove.Length;
        }

        int RuinSemiStrategic()
        {
            if (domain.Domain.Categories.Length == 0)
                return 0;

            var removedTasks = 0;
            var category = domain.Domain.Categories.RandomElement(_random);
            foreach(var day in domain.PlanningDays.Where(_ => _random.RandomBool()))
            {
                var tasksToRemove = day.ScheduledTasks.Where(c => c.Task.Categories.Contains(category)).ToArray();
                foreach (var task in tasksToRemove)
                    day.RemoveScheduledTask(task);

                removedTasks += tasksToRemove.Length;
            }

            return removedTasks;
        }

        int RuinStrategic()
        {
            var categories = domain.Domain.Categories.Where(c => _random.NextDouble() < 0.1).ToArray();
            if (categories.Length == 0)
                return 0;

            var removedTasks = 0;
            foreach (var day in domain.PlanningDays.Where(_ => _random.RandomBool()))
            {
                var tasksToRemove = day.ScheduledTasks.Where(c => c.Task.Categories.Overlaps(categories)).ToArray();
                foreach (var task in tasksToRemove)
                    day.RemoveScheduledTask(task);

                removedTasks += tasksToRemove.Length;
            }

            return removedTasks;
        }
    }

    public enum MoveScope
    {
        Operational,    //one category one day
        Tactical,       //multiple categories one day
        SemiStrategic,  //one category multiple days
        Strategic       //multiple categories multiple days
    }

    public enum MoveType
    {
        Add,
        Remove,
        Swap,
        CascadeMove,
        RuinRecreate
    }
}
