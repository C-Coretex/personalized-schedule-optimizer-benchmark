using Web.Features.Schedule.Models.Tasks;

namespace Web.Features.Schedule.Endpoints.Generate;

public class Validator
{
    public static bool IsValid(Request request, out string? error)
    {
        return ValidatePlanningHorizon(request, out error)
                && ValidateFixedTasks(request, out error)
                && ValidateDynamicTasks(request, out error)
                && ValidateCategoryWindows(request, out error)
                && ValidateDifficultyCapacities(request, out error)
                && ValidateTaskTypePreferences(request, out error);
    }

    private static bool ValidatePlanningHorizon(Request request, out string? error)
    {
        error = null;

        if (request.PlanningHorizon.StartDate >= request.PlanningHorizon.EndDate)
        {
            error = "PlanningHorizon.StartDate must be before EndDate.";
            return false;
        }

        return true;
    }

    private static bool ValidateFixedTasks(Request request, out string? error)
    {
        error = null;
        var horizon = request.PlanningHorizon;

        for (var i = 0; i < request.FixedTasks.Count; i++)
        {
            var task = request.FixedTasks[i];
            var prefix = $"FixedTasks[{i}]";

            if (!ValidateTaskBase(task, prefix, out error)) return false;

            if (task.StartTime >= task.EndTime)
            {
                error = $"{prefix}.StartTime must be before EndTime.";
                return false;
            }

            if (DateOnly.FromDateTime(task.StartTime) < horizon.StartDate ||
                DateOnly.FromDateTime(task.EndTime) > horizon.EndDate)
            {
                error = $"{prefix} must fall within the planning horizon ({horizon.StartDate} – {horizon.EndDate}).";
                return false;
            }

            if(task.StartTime.Date != task.EndTime.Date)
            {
                error = $"{prefix} must start and end on the same day.";
                return false;
            }
        }


        //check that fixed tasks cannot overlap with each other
        var fixedTasks = request.FixedTasks.OrderBy(ft => ft.StartTime).ToArray();
        for(var i = 0; i < fixedTasks.Length - 1; i++)
        {
            if(fixedTasks[i].EndTime > fixedTasks[i + 1].StartTime)
            {
                error = $"FixedTasks[{i}] overlaps with FixedTasks[{i + 1}].";
                return false;
            }
        }

        return true;
    }

    private static bool ValidateDynamicTasks(Request request, out string? error)
    {
        error = null;
        var horizon = request.PlanningHorizon;

        for (var i = 0; i < request.DynamicTasks.Count; i++)
        {
            var task = request.DynamicTasks[i];
            var prefix = $"DynamicTasks[{i}]";

            if (!ValidateTaskBase(task, prefix, out error)) return false;

            if (task.Duration <= 0)
            {
                error = $"{prefix}.Duration must be greater than 0.";
                return false;
            }

            if (task.WindowStart.HasValue && task.WindowEnd.HasValue &&
                task.WindowStart.Value >= task.WindowEnd.Value)
            {
                error = $"{prefix}.WindowStart must be before WindowEnd.";
                return false;
            }

            if (task.Deadline.HasValue &&
                (DateOnly.FromDateTime(task.Deadline.Value) < horizon.StartDate ||
                 DateOnly.FromDateTime(task.Deadline.Value) > horizon.EndDate))
            {
                error = $"{prefix}.Deadline must fall within the planning horizon ({horizon.StartDate} – {horizon.EndDate}).";
                return false;
            }

            if (task.Repeating is { } r)
            {
                if (r.MinDayCount < 0 || r.OptDayCount < 0 || r.MinWeekCount < 0 || r.OptWeekCount < 0)
                {
                    error = $"{prefix}.Repeating counts must be >= 0.";
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ValidateCategoryWindows(Request request, out string? error)
    {
        error = null;

        for (var i = 0; i < request.CategoryWindows.Count; i++)
        {
            var cw = request.CategoryWindows[i];
            var prefix = $"CategoryWindows[{i}]";

            if (cw.StartDateTime >= cw.EndDateTime)
            {
                error = $"{prefix}.StartDateTime must be before EndDateTime.";
                return false;
            }
        }

        return true;
    }

    private static bool ValidateDifficultyCapacities(Request request, out string? error)
    {
        error = null;

        for (var i = 0; i < request.DifficultyCapacities.Count; i++)
        {
            var entry = request.DifficultyCapacities[i];

            if (entry.Capacity < 0)
            {
                error = $"DifficultyCapacities[{i}].Capacity must be >= 0.";
                return false;
            }
        }

        return true;
    }

    private static bool ValidateTaskTypePreferences(Request request, out string? error)
    {
        error = null;

        for (var i = 0; i < request.TaskTypePreferences.Count; i++)
        {
            var entry = request.TaskTypePreferences[i];

            for (var j = 0; j < entry.Preferences.Count; j++)
            {
                if (entry.Preferences[j].Weight < 0)
                {
                    error = $"TaskTypePreferences[{i}].Preferences[{j}].Weight must be >= 0.";
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ValidateTaskBase(TaskBase task, string prefix, out string? error)
    {
        error = null;

        if (task.Priority is < 1 or > 5)
        {
            error = $"{prefix}.Priority must be between 1 and 5.";
            return false;
        }

        if (task.Difficulty is < 1 or > 10)
        {
            error = $"{prefix}.Difficulty must be between 1 and 10.";
            return false;
        }

        return true;
    }
}
