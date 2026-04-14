package com.pso.timefold.solver;

import com.pso.timefold.domain.*;
import com.pso.timefold.domain.enums.DifficultTaskSchedulingStrategy;
import com.pso.timefold.dto.GenerateScheduleRequest;
import com.pso.timefold.dto.ScheduledTask;

import java.time.DayOfWeek;
import java.time.LocalDate;
import java.time.LocalDateTime;
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
     * The planning variable domain is a single shared CountableValueRange on
     * ScheduleSolution covering [0, horizonMinutes) at 1-minute granularity.
     * HC3/HC4/HC5/HC8 are enforced as hard constraints rather than by pre-filtering.
     *
     * Also pre-computes:
     *   - WeekRequirements (one per repeating task × week in horizon) for HC6/SC5
     *   - DayRequirements (one per repeating task × day in horizon) for HC7/SC6
     *   - DayFacts (one per day in horizon) for SC7
     *   - fixedDifficulty on each DifficultyCapacityEntry for SC2
     */
    public static ScheduleSolution buildSolution(GenerateScheduleRequest request) {
        ScheduleSolution solution = new ScheduleSolution();

        List<FixedTask> fixedTasks = request.getFixedTasks() != null
                ? request.getFixedTasks() : Collections.emptyList();
        List<CategoryWindow> categoryWindows = request.getCategoryWindows() != null
                ? request.getCategoryWindows() : Collections.emptyList();
        List<DynamicTask> dynamicTasks = request.getDynamicTasks() != null
                ? request.getDynamicTasks() : Collections.emptyList();

        solution.setFixedTasks(fixedTasks);
        solution.setDynamicTasks(dynamicTasks);
        solution.setPlanningHorizon(request.getPlanningHorizon());
        solution.setCategoryWindows(categoryWindows);

        // Wrap strategy in a single-element list so constraint streams can forEach over it
        DifficultTaskSchedulingStrategy strategy = request.getDifficultTaskSchedulingStrategy();
        solution.setStrategies(strategy != null ? List.of(strategy) : Collections.emptyList());

        LocalDate horizonStart = request.getPlanningHorizon() != null
                ? request.getPlanningHorizon().getStartDate()
                : LocalDate.now();
        LocalDate horizonEnd = request.getPlanningHorizon() != null
                ? request.getPlanningHorizon().getEndDate()
                : horizonStart.plusDays(6);
        long horizonDays = request.getPlanningHorizon() != null
                ? request.getPlanningHorizon().getDays()
                : 7L;
        int horizonMinutes = (int) horizonDays * 24 * 60;

        // --- Pre-compute fixedDifficulty on DifficultyCapacityEntry ---
        List<DifficultyCapacityEntry> difficultyCapacities = new ArrayList<>();
        if (request.getDifficultyCapacities() != null) {
            for (DifficultyCapacityEntry entry : request.getDifficultyCapacities()) {
                int fixedDiff = 0;
                for (FixedTask ft : fixedTasks) {
                    if (ft.getStartTime() != null
                            && ft.getStartTime().toLocalDate().equals(entry.getDate())) {
                        fixedDiff += ft.getDifficulty();
                    }
                }
                entry.setFixedDifficulty(fixedDiff);
                difficultyCapacities.add(entry);
            }
        }
        solution.setDifficultyCapacities(difficultyCapacities);

        solution.setTaskTypePreferences(request.getTaskTypePreferences() != null
                ? request.getTaskTypePreferences() : Collections.emptyList());

        // --- Build TaskAssignments ---
        List<TaskAssignment> assignments = new ArrayList<>();
        for (DynamicTask task : dynamicTasks) {
            if (task.getId() == null) {
                task.setId(UUID.randomUUID());
            }

            int occurrences = Math.min(task.maxOccurrences(), (int) horizonDays);
            for (int i = 0; i < occurrences; i++) {
                String assignmentId = task.getId().toString() + "-occ" + i;
                assignments.add(new TaskAssignment(assignmentId, task, i, horizonStart));
            }
        }
        solution.setTaskAssignments(assignments);

        // --- Compute DayFacts (one per horizon day) ---
        List<DayFact> dayFacts = new ArrayList<>();
        for (int d = 0; d < horizonDays; d++) {
            LocalDate day = horizonStart.plusDays(d);
            int fixedDiffForDay = 0;
            for (FixedTask ft : fixedTasks) {
                if (ft.getStartTime() != null && ft.getStartTime().toLocalDate().equals(day)) {
                    fixedDiffForDay += ft.getDifficulty();
                }
            }
            dayFacts.add(new DayFact(day, fixedDiffForDay));
        }
        solution.setDayFacts(dayFacts);

        // --- Compute WeekRequirements and DayRequirements for repeating tasks ---
        List<WeekRequirement> weekRequirements = new ArrayList<>();
        List<DayRequirement> dayRequirements = new ArrayList<>();

        List<DynamicTask> repeatingTasks = dynamicTasks.stream()
                .filter(t -> t.getRepeating() != null)
                .toList();

        // DayRequirements: one per (repeating task, day in horizon)
        for (DynamicTask task : repeatingTasks) {
            for (int d = 0; d < horizonDays; d++) {
                LocalDate day = horizonStart.plusDays(d);
                dayRequirements.add(new DayRequirement(
                        task, day,
                        task.getRepeating().getMinDayCount(),
                        task.getRepeating().getOptDayCount()));
            }
        }

        // WeekRequirements: iterate ISO weeks overlapping the horizon
        if (!repeatingTasks.isEmpty()) {
            // Start from the Monday of the week containing horizonStart
            LocalDate weekCursor = horizonStart.with(DayOfWeek.MONDAY);
            // If that Monday is after horizonStart (shouldn't happen), step back
            if (weekCursor.isAfter(horizonStart)) {
                weekCursor = weekCursor.minusWeeks(1);
            }

            while (!weekCursor.isAfter(horizonEnd)) {
                // Clamp to horizon boundaries
                LocalDate weekStart = weekCursor.isBefore(horizonStart) ? horizonStart : weekCursor;
                LocalDate weekEnd = weekCursor.plusDays(6).isAfter(horizonEnd)
                        ? horizonEnd : weekCursor.plusDays(6);

                for (DynamicTask task : repeatingTasks) {
                    weekRequirements.add(new WeekRequirement(
                            task, weekStart, weekEnd,
                            task.getRepeating().getMinWeekCount(),
                            task.getRepeating().getOptWeekCount()));
                }
                weekCursor = weekCursor.plusWeeks(1);
            }
        }

        solution.setWeekRequirements(weekRequirements);
        solution.setDayRequirements(dayRequirements);

        return solution;
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
