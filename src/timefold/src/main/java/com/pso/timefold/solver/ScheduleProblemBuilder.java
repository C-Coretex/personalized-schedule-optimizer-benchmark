package com.pso.timefold.solver;

import com.pso.timefold.domain.*;
import com.pso.timefold.dto.GenerateScheduleRequest;
import com.pso.timefold.dto.ScheduledTask;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.temporal.ChronoUnit;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

/**
 * Shared utility for converting between GenerateScheduleRequest and ScheduleSolution.
 * Used by both SolverService (Spring Boot API) and ScheduleConsoleApp (CLI).
 */
public final class ScheduleProblemBuilder {

    private ScheduleProblemBuilder() {}

    /**
     * Builds an initial ScheduleSolution from a GenerateScheduleRequest.
     *
     * For each DynamicTask:
     * - Non-repeating tasks → 1 TaskAssignment (startMinute = null)
     * - Repeating tasks → maxOccurrences() TaskAssignments capped at horizon days
     *
     * Each TaskAssignment receives a pre-computed list of valid start minutes
     * (1-minute granularity) that excludes:
     *   - Fixed-task overlaps (partial HC1)
     *   - Time-of-day window violations (HC3)
     *   - Deadline violations (HC4)
     *   - Category-window violations (HC5)
     *   - Out-of-horizon slots (HC8)
     *
     * All TaskAssignments start unscheduled (startMinute = null).
     */
    public static ScheduleSolution buildSolution(GenerateScheduleRequest request) {
        ScheduleSolution solution = new ScheduleSolution();

        List<FixedTask> fixedTasks = request.getFixedTasks() != null
                ? request.getFixedTasks() : Collections.emptyList();
        List<CategoryWindow> categoryWindows = request.getCategoryWindows() != null
                ? request.getCategoryWindows() : Collections.emptyList();

        solution.setFixedTasks(fixedTasks);
        solution.setDynamicTasks(request.getDynamicTasks() != null
                ? request.getDynamicTasks() : Collections.emptyList());
        solution.setPlanningHorizon(request.getPlanningHorizon());
        solution.setCategoryWindows(categoryWindows);
        solution.setDifficultTaskSchedulingStrategy(request.getDifficultTaskSchedulingStrategy());
        solution.setDifficultyCapacities(request.getDifficultyCapacities() != null
                ? request.getDifficultyCapacities() : Collections.emptyList());
        solution.setTaskTypePreferences(request.getTaskTypePreferences() != null
                ? request.getTaskTypePreferences() : Collections.emptyList());

        LocalDate horizonStart = request.getPlanningHorizon() != null
                ? request.getPlanningHorizon().getStartDate()
                : LocalDate.now();
        long horizonDays = request.getPlanningHorizon() != null
                ? request.getPlanningHorizon().getDays()
                : 7L;
        int horizonMinutes = (int) horizonDays * 24 * 60;

        List<TaskAssignment> assignments = new ArrayList<>();
        if (request.getDynamicTasks() != null) {
            for (DynamicTask task : request.getDynamicTasks()) {
                // Ensure task has an id — generate one if the JSON omitted it
                if (task.getId() == null) {
                    task.setId(UUID.randomUUID());
                }

                List<Integer> validStartMinutes = computeValidStartMinutes(
                        task, fixedTasks, categoryWindows, horizonStart, horizonMinutes);

                int occurrences = Math.min(task.maxOccurrences(), (int) horizonDays);
                for (int i = 0; i < occurrences; i++) {
                    String assignmentId = task.getId().toString() + "-occ" + i;
                    assignments.add(new TaskAssignment(assignmentId, task, i, validStartMinutes));
                }
            }
        }
        solution.setTaskAssignments(assignments);

        return solution;
    }

    /**
     * Pre-computes the list of feasible start minutes for a task at 1-minute granularity.
     *
     * Instead of checking every minute individually, this method first computes
     * the set of effective valid ranges by intersecting:
     *   - category windows (HC5) — for tasks with categories
     *   - task time-of-day window (HC3): effectiveStart = max(cwStart, dayBase + windowStart)
     *                                    effectiveEnd   = min(cwEnd,   dayBase + windowEnd)
     *   - deadline (HC4):                effectiveEnd   = min(effectiveEnd, deadlineMin)
     *   - planning horizon (HC8): guaranteed by loop bound and horizon clamp
     *
     * Slots within each effective range are then filtered for fixed-task overlap (HC1 partial).
     *
     * Mirrors the intersection logic in specialized/Task.cs → freeTaskTimeWindows.
     */
    private static List<Integer> computeValidStartMinutes(
            DynamicTask task,
            List<FixedTask> fixedTasks,
            List<CategoryWindow> categoryWindows,
            LocalDate horizonStart,
            int horizonMinutes) {

        int duration = task.getDurationMinutes();
        if (duration <= 0) return Collections.emptyList();

        LocalDateTime horizonOrigin = horizonStart.atStartOfDay();

        // Pre-compute fixed task boundaries in horizon-relative minutes
        int[][] fixedIntervals = new int[fixedTasks.size()][2];
        for (int i = 0; i < fixedTasks.size(); i++) {
            FixedTask ft = fixedTasks.get(i);
            fixedIntervals[i][0] = (int) ChronoUnit.MINUTES.between(horizonOrigin, ft.getStartTime());
            fixedIntervals[i][1] = (int) ChronoUnit.MINUTES.between(horizonOrigin, ft.getEndTime());
        }

        // Task time-of-day window in minutes-of-day (0..1440)
        int windowStartMin = task.getWindowStart() != null
                ? task.getWindowStart().getHour() * 60 + task.getWindowStart().getMinute() : 0;
        int windowEndMin = task.getWindowEnd() != null
                ? task.getWindowEnd().getHour() * 60 + task.getWindowEnd().getMinute() : 24 * 60;

        // HC4: deadline in horizon-relative minutes (Integer.MAX_VALUE = no deadline)
        int deadlineMin = task.getDeadline() != null
                ? (int) ChronoUnit.MINUTES.between(horizonOrigin, task.getDeadline()) : horizonMinutes;

        boolean hasCategories = task.getCategories() != null && !task.getCategories().isEmpty();
        int horizonDays = horizonMinutes / (24 * 60);

        // Step 1: compute effective valid ranges [rangeStart, lastValidStart] by intersection.
        // Each range element is int[2]: { first valid slot start, last valid slot start }.
        List<int[]> validRanges = new ArrayList<>();

        if (hasCategories) {
            // For each category window that matches this task's categories, intersect
            // the window with the task's time-of-day window and deadline (per day).
            for (CategoryWindow cw : categoryWindows) {
                if (!task.getCategories().contains(cw.getCategory())) continue;

                int cwStart = (int) ChronoUnit.MINUTES.between(horizonOrigin, cw.getStartDateTime());
                int cwEnd   = (int) ChronoUnit.MINUTES.between(horizonOrigin, cw.getEndDateTime());

                // Clamp to horizon
                cwStart = Math.max(cwStart, 0);
                cwEnd   = Math.min(cwEnd, horizonMinutes);
                if (cwStart >= cwEnd) continue;

                // Iterate each day covered by this category window
                int dStart = cwStart / (24 * 60);
                int dEnd   = Math.min((cwEnd - 1) / (24 * 60), horizonDays - 1);

                for (int d = dStart; d <= dEnd; d++) {
                    int dayBase = d * 24 * 60;

                    // Intersect: take the later start and the earlier end
                    int effectiveStart = Math.max(cwStart, dayBase + windowStartMin);
                    int effectiveEnd   = Math.min(cwEnd,   dayBase + windowEndMin);

                    // Apply deadline (HC4)
                    effectiveEnd = Math.min(effectiveEnd, deadlineMin);

                    // Last valid slot start = effectiveEnd - duration
                    if (effectiveEnd - effectiveStart >= duration) {
                        validRanges.add(new int[]{effectiveStart, effectiveEnd - duration});
                    }
                }
            }
        } else {
            // No category constraint: apply time-of-day window + deadline per day
            for (int d = 0; d < horizonDays; d++) {
                int dayBase = d * 24 * 60;

                int effectiveStart = dayBase + windowStartMin;
                int effectiveEnd   = dayBase + windowEndMin;

                // Apply deadline (HC4)
                effectiveEnd = Math.min(effectiveEnd, deadlineMin);

                if (effectiveEnd - effectiveStart >= duration) {
                    validRanges.add(new int[]{effectiveStart, effectiveEnd - duration});
                }
            }
        }

        if (validRanges.isEmpty()) return Collections.emptyList();

        // Step 2: collect individual slot minutes from each range, excluding fixed-task overlaps (HC1 partial)
        List<Integer> valid = new ArrayList<>();
        for (int[] range : validRanges) {
            for (int s = range[0]; s <= range[1]; s++) {
                boolean overlapsFixed = false;
                for (int[] ft : fixedIntervals) {
                    if (s < ft[1] && s + duration > ft[0]) {
                        overlapsFixed = true;
                        break;
                    }
                }
                if (!overlapsFixed) valid.add(s);
            }
        }

        return valid;
    }

    /**
     * Converts a solved ScheduleSolution back into a flat list of ScheduledTask DTOs.
     *
     * Includes:
     * - Scheduled TaskAssignments (startMinute != null)
     * - All FixedTasks (always present)
     */
    public static List<ScheduledTask> extractTimeline(ScheduleSolution solution) {
        List<ScheduledTask> timeline = new ArrayList<>();

        LocalDate horizonStart = solution.getPlanningHorizon() != null
                ? solution.getPlanningHorizon().getStartDate()
                : LocalDate.now();

        // Dynamic scheduled tasks
        if (solution.getTaskAssignments() != null) {
            for (TaskAssignment assignment : solution.getTaskAssignments()) {
                if (assignment.getStartMinute() == null) continue;

                DynamicTask task = assignment.getTask();
                if (task.getId() == null) task.setId(UUID.randomUUID());
                LocalDateTime startTime = horizonStart.atStartOfDay()
                        .plusMinutes(assignment.getStartMinute());
                LocalDateTime endTime = startTime.plusMinutes(task.getDurationMinutes());

                timeline.add(new ScheduledTask(
                        task.getId(),
                        task.getName(),
                        startTime,
                        endTime,
                        task.getPriority(),
                        task.getDifficulty(),
                        false
                ));
            }
        }

        // Fixed tasks (always scheduled, exact times)
        if (solution.getFixedTasks() != null) {
            for (FixedTask task : solution.getFixedTasks()) {
                if (task.getId() == null) task.setId(UUID.randomUUID());
                timeline.add(new ScheduledTask(
                        task.getId(),
                        task.getName(),
                        task.getStartTime(),
                        task.getEndTime(),
                        task.getPriority(),
                        task.getDifficulty(),
                        true
                ));
            }
        }

        return timeline;
    }
}
