package com.pso.timefold.domain;

import lombok.AllArgsConstructor;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.LocalDate;

/**
 * Problem fact: represents the weekly scheduling requirement for a repeating DynamicTask.
 * One instance is created for each (repeating task, week in planning horizon) pair.
 * Used by HC6 and SC5 constraints.
 *
 * weekStart and weekEnd are clamped to the planning horizon boundaries,
 * so partial weeks at the edges still generate a requirement.
 */
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
public class WeekRequirement {

    /** The repeating task this requirement belongs to. */
    private DynamicTask task;

    /** First day of the week (Monday, clamped to horizonStart if this is the first week). */
    private LocalDate weekStart;

    /** Last day of the week (Sunday, clamped to horizonEnd if this is the last week). */
    private LocalDate weekEnd;

    /** Minimum required occurrences in this week (task.repeating.minWeekCount). */
    private int minWeekCount;

    /** Optimal (target) occurrences in this week (task.repeating.optWeekCount). */
    private int optWeekCount;
}
