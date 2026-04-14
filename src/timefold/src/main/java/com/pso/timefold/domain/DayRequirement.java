package com.pso.timefold.domain;

import lombok.AllArgsConstructor;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.LocalDate;

/**
 * Problem fact: represents the daily scheduling requirement for a repeating DynamicTask.
 * One instance is created for each (repeating task, day in planning horizon) pair.
 * Used by HC7 and SC6 constraints.
 */
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
public class DayRequirement {

    /** The repeating task this requirement belongs to. */
    private DynamicTask task;

    /** The calendar day this requirement applies to. */
    private LocalDate date;

    /** Minimum required occurrences on this day (task.repeating.minDayCount). */
    private int minDayCount;

    /** Optimal (target) occurrences on this day (task.repeating.optDayCount). */
    private int optDayCount;
}
