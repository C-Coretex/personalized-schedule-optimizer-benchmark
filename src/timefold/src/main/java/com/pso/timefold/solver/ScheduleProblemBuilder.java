package com.pso.timefold.solver;

import com.pso.timefold.domain.*;
import com.pso.timefold.dto.GenerateScheduleRequest;
import com.pso.timefold.dto.ScheduledTask;

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
     * All TaskAssignments start unscheduled (startMinute = null).
     */
    public static ScheduleSolution buildSolution(GenerateScheduleRequest request) {
        ScheduleSolution solution = new ScheduleSolution();

        solution.setFixedTasks(request.getFixedTasks() != null
                ? request.getFixedTasks() : Collections.emptyList());
        solution.setDynamicTasks(request.getDynamicTasks() != null
                ? request.getDynamicTasks() : Collections.emptyList());
        solution.setPlanningHorizon(request.getPlanningHorizon());
        solution.setCategoryWindows(request.getCategoryWindows() != null
                ? request.getCategoryWindows() : Collections.emptyList());
        solution.setDifficultTaskSchedulingStrategy(request.getDifficultTaskSchedulingStrategy());
        solution.setDifficultyCapacities(request.getDifficultyCapacities() != null
                ? request.getDifficultyCapacities() : Collections.emptyList());
        solution.setTaskTypePreferences(request.getTaskTypePreferences() != null
                ? request.getTaskTypePreferences() : Collections.emptyList());

        long horizonDays = request.getPlanningHorizon() != null
                ? request.getPlanningHorizon().getDays()
                : 7L;

        List<TaskAssignment> assignments = new ArrayList<>();
        if (request.getDynamicTasks() != null) {
            for (DynamicTask task : request.getDynamicTasks()) {
                // Ensure task has an id — generate one if the JSON omitted it
                if (task.getId() == null) {
                    task.setId(UUID.randomUUID());
                }
                int occurrences = Math.min(task.maxOccurrences(), (int) horizonDays);
                for (int i = 0; i < occurrences; i++) {
                    String assignmentId = task.getId().toString() + "-occ" + i;
                    assignments.add(new TaskAssignment(assignmentId, task, i));
                }
            }
        }
        solution.setTaskAssignments(assignments);

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
