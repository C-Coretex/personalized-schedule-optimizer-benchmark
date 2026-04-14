package com.pso.timefold.domain;

import ai.timefold.solver.core.api.domain.entity.PlanningEntity;
import ai.timefold.solver.core.api.domain.lookup.PlanningId;
import ai.timefold.solver.core.api.domain.variable.PlanningVariable;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

/**
 * Planning entity: one scheduled occurrence slot for a DynamicTask.
 * Multiple instances are created per repeating task (one per max occurrence).
 *
 * The single planning variable is {@code startMinute}: integer minutes from
 * 00:00 on the first day of the planning horizon. Null means unscheduled.
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
     * Value range is provided by ScheduleSolution.getStartMinuteRange() at 15-minute granularity.
     */
    @PlanningVariable(allowsUnassigned = true)
    private Integer startMinute;

    public TaskAssignment(String id, DynamicTask task, int occurrenceIndex) {
        this.id = id;
        this.task = task;
        this.occurrenceIndex = occurrenceIndex;
    }

    /**
     * End minute in horizon-relative minutes. Null if unscheduled.
     */
    public Integer getEndMinute() {
        if (startMinute == null) return null;
        return startMinute + task.getDurationMinutes();
    }
}
