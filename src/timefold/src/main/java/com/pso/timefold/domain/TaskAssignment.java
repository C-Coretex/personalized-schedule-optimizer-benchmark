package com.pso.timefold.domain;

import ai.timefold.solver.core.api.domain.entity.PlanningEntity;
import ai.timefold.solver.core.api.domain.lookup.PlanningId;
import ai.timefold.solver.core.api.domain.valuerange.ValueRangeProvider;
import ai.timefold.solver.core.api.domain.variable.PlanningVariable;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.util.Collections;
import java.util.List;

/**
 * Planning entity: one scheduled occurrence slot for a DynamicTask.
 * Multiple instances are created per repeating task (one per max occurrence).
 *
 * The single planning variable is {@code startMinute}: integer minutes from
 * 00:00 on the first day of the planning horizon. Null means unscheduled.
 *
 * The domain for startMinute is the per-entity {@code validStartMinutes} list,
 * pre-computed by ScheduleProblemBuilder to contain only feasible slots
 * (respecting fixed-task overlaps, time windows, deadlines, and category windows).
 */
@PlanningEntity
@Getter
@Setter
@NoArgsConstructor
public class TaskAssignment {

    /** Unique id, format: "<taskUuid>-occ<index>" */
    @PlanningId
    private String id;

    /** The task this assignment belongs to (problem fact, not a planning variable). */
    private DynamicTask task;

    /** Zero-based occurrence index (for repeating tasks). */
    private int occurrenceIndex;

    /**
     * Minutes from midnight of planningHorizon.startDate.
     * Null = unscheduled (allowsUnassigned = true).
     * Domain is provided by getValidStartMinutes() at 1-minute granularity.
     */
    @PlanningVariable(allowsUnassigned = true)
    private Integer startMinute;

    /**
     * Pre-computed feasible start minutes for this task.
     * Set by ScheduleProblemBuilder at problem initialization; never mutated by the solver.
     * Filters out: fixed-task overlaps, time-window violations, deadline violations,
     * category-window violations, and out-of-horizon slots.
     */
    private List<Integer> validStartMinutes;

    public TaskAssignment(String id, DynamicTask task, int occurrenceIndex, List<Integer> validStartMinutes) {
        this.id = id;
        this.task = task;
        this.occurrenceIndex = occurrenceIndex;
        this.validStartMinutes = validStartMinutes;
    }

    /** Per-entity value range: only feasible start minutes for this specific task. */
    @ValueRangeProvider
    public List<Integer> getValidStartMinutes() {
        return validStartMinutes != null ? validStartMinutes : Collections.emptyList();
    }

    /**
     * End minute in horizon-relative minutes. Null if unscheduled.
     */
    public Integer getEndMinute() {
        if (startMinute == null) return null;
        return startMinute + task.getDurationMinutes();
    }
}
