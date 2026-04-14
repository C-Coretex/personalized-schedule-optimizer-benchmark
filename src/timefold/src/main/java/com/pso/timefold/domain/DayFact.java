package com.pso.timefold.domain;

import lombok.AllArgsConstructor;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.LocalDate;

/**
 * Problem fact: represents one calendar day in the planning horizon.
 * One instance is created per day. Used by SC7 (difficulty imbalance).
 *
 * fixedDifficulty is pre-computed at build time as the sum of difficulties
 * of all FixedTasks whose start falls on this day, so SC7 can account for
 * the constant fixed-task contribution without joining against FixedTask at
 * solve time.
 */
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
public class DayFact {

    /** Calendar date this fact represents. */
    private LocalDate date;

    /** Sum of difficulties of FixedTasks scheduled on this day (constant). */
    private int fixedDifficulty;
}
