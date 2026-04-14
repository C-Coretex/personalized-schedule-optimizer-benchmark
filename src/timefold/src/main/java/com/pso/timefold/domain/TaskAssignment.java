package com.pso.timefold.domain;

import ai.timefold.solver.core.api.domain.entity.PlanningEntity;
import ai.timefold.solver.core.api.domain.lookup.PlanningId;
import ai.timefold.solver.core.api.domain.variable.PlanningVariable;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.DayOfWeek;
import java.time.LocalDate;

/**
 * Planning entity: one scheduled occurrence slot for a DynamicTask.
 * Multiple instances are created per repeating task (one per max occurrence).
 *
 * The single planning variable is {@code startMinute}: integer minutes from
 * 00:00 on the first day of the planning horizon. Null means unscheduled.
 *
 * The value range is a single shared CountableValueRange on ScheduleSolution
 * covering [0, horizonMinutes) at 1-minute granularity. This is lazy and
 * non-materialized, so memory use is O(1) regardless of horizon length.
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
     * Value range is provided by ScheduleSolution.getStartMinuteRange().
     */
    @PlanningVariable(allowsUnassigned = true)
    private Integer startMinute;

    /**
     * First day of the planning horizon (set by ScheduleProblemBuilder).
     * Used by helper methods to convert startMinute to calendar date/time-of-day,
     * and by HC4/HC5 constraints that need to convert LocalDateTime facts to
     * horizon-relative minutes.
     */
    private LocalDate horizonStart;

    public TaskAssignment(String id, DynamicTask task, int occurrenceIndex, LocalDate horizonStart) {
        this.id = id;
        this.task = task;
        this.occurrenceIndex = occurrenceIndex;
        this.horizonStart = horizonStart;
    }

    /**
     * End minute in horizon-relative minutes. Null if unscheduled.
     */
    public Integer getEndMinute() {
        if (startMinute == null) return null;
        return startMinute + task.getDurationMinutes();
    }

    /**
     * Zero-based day index within the planning horizon.
     * Returns -1 if unscheduled.
     */
    public int getDayOffset() {
        if (startMinute == null) return -1;
        return startMinute / (24 * 60);
    }

    /**
     * Calendar date of this assignment within the planning horizon.
     * Returns null if unscheduled or horizonStart is not set.
     */
    public LocalDate getTaskDay() {
        if (startMinute == null || horizonStart == null) return null;
        return horizonStart.plusDays(getDayOffset());
    }

    /**
     * Monday of the ISO week this assignment falls in.
     * Returns null if unscheduled or horizonStart is not set.
     */
    public LocalDate getTaskWeek() {
        if (startMinute == null || horizonStart == null) return null;
        return getTaskDay().with(DayOfWeek.MONDAY);
    }

    /**
     * Minutes elapsed since midnight on the assignment's day (time-of-day start).
     * Returns -1 if unscheduled.
     */
    public int getTimeOfDayStart() {
        if (startMinute == null) return -1;
        return startMinute % (24 * 60);
    }

    /**
     * End time as minutes since midnight on the assignment's day.
     * Returns -1 if unscheduled.
     */
    public int getTimeOfDayEnd() {
        if (startMinute == null) return -1;
        return getTimeOfDayStart() + task.getDurationMinutes();
    }
}
